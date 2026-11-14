using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;


namespace Z1R_Sync
{
    public static class SyncManager
    {
        private static string _AzureFunctionUrl;

        private static readonly HttpClient _httpClient = new HttpClient();
        private static HubConnection _connection;

        // Delegates for handling different kinds of messages
        private static Action<string, string, string> _onTileChange;
        private static Action<string, string, string> _onSyncMessage;

        private static Action<string, string, string, long> _onSyncMessageWithTimestamp;

        private static readonly ConcurrentQueue<SyncMessage> _outbox = new ConcurrentQueue<SyncMessage>();
        private static bool _sending = false;
        private static readonly object _sendLock = new object();
        private static int _retryDelayMs = 1000; // base delay for retries

        public static string SafeNormalizeUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            // Trim whitespace
            string url = input.Trim();

            // Remove any trailing slashes
            url = url.TrimEnd('/');

            // Fix accidental double slashes (except after protocol, e.g. https://)
            url = Regex.Replace(url, "(?<!:)/{2,}", "/");

            // Ensure it's a valid absolute URI
            if (Uri.TryCreate(url, UriKind.Absolute, out Uri result))
            {
                return result.ToString();
            }
            else
            {
                // Fall back to raw cleaned version
                return url;
            }
        }

        // Test seam: when set, outgoing messages go here instead of the network (the self-test uses it to see what would be sent). Null in normal use.
        public static Action<string, string, string> TestSendHook = null;

        public static void EnqueueSync(string msgType, string payload, string senderId)
        {
            var hook = TestSendHook;
            if (hook != null) { hook(msgType, payload, senderId); return; }
            var message = new SyncMessage
            {
                messageType = msgType,
                payload = payload,
                senderId = senderId,
                timeStamp = DateTime.UtcNow.Ticks  // include a timestamp or guid
            };
            _outbox.Enqueue(message);
            StartSenderLoop();
        }

        private static void StartSenderLoop()
        {
            lock (_sendLock)
            {
                if (_sending) return;
                _sending = true;
                // Run the send loop on a background thread (Task)
                Task.Run(async () => await ProcessQueueAsync());
            }
        }

        // Most messages carry a whole state, so when the host is down for long, the oldest are dropped rather than kept without limit.
        private const int MaxOutbox = 1000;

        private static async Task ProcessQueueAsync()
        {
            while (true)
            {
                // (only this loop dequeues, so dropping here cannot remove a message that is being sent)
                while (_outbox.Count > MaxOutbox && _outbox.TryDequeue(out SyncMessage dropped))
                    Console.WriteLine($"[Sync] Outbox full, dropped an old {dropped.messageType} message");
                if (!_outbox.TryPeek(out SyncMessage msg))
                    break;
                var result = await TrySendMessageAsync(msg);
                if (result != SendResult.Retry)
                {
                    _outbox.TryDequeue(out _);  // sent, or refused for good: either way it is done
                    _retryDelayMs = 1000;       // reset delay after success
                }
                else
                {
                    // Exponential backoff before retrying the same message
                    await Task.Delay(_retryDelayMs);
                    _retryDelayMs = Math.Min(_retryDelayMs * 2, 15000);  // cap the backoff (e.g., 15s max)
                }
            }
            // Queue is empty, stop the loop
            lock (_sendLock)
            {
                _sending = false;
                // (a message queued after the last peek but before _sending was cleared would wait for the next one: start again for it)
                if (!_outbox.IsEmpty)
                {
                    _sending = true;
                    Task.Run(async () => await ProcessQueueAsync());
                }
            }
        }

        private enum SendResult { Sent, Retry, Drop }

        private static async Task<SendResult> TrySendMessageAsync(SyncMessage message)
        {
            try
            {
                // Example using existing HTTP post to Azure Function (same as current Send logic)
                var json = JsonConvert.SerializeObject(new
                {
                    messageType = message.messageType,
                    payload = message.payload,
                    senderId = message.senderId,
                    timeStamp = message.timeStamp
                });
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage resp = await _httpClient.PostAsync(AzureFunctionUrl, content);
                    if (resp.StatusCode == (HttpStatusCode)429)
                    {
                        // Too Many Requests: don't consider as sent, but handle retry after
                        if (resp.Headers.TryGetValues("Retry-After", out var values))
                        {
                            string retryAfterSec = values.FirstOrDefault();
                            Console.WriteLine("[Sync] Throttled, server said retry after " + retryAfterSec + " seconds.");
                        }
                        else
                        {
                            Console.WriteLine("[Sync] Throttled with 429, no Retry-After header.");
                        }
                        return SendResult.Retry; // will trigger a retry with backoff
                    }
                    if (!resp.IsSuccessStatusCode)
                    {
                        string err = await resp.Content.ReadAsStringAsync();
                        Console.WriteLine($"[Sync] Send failed: HTTP {resp.StatusCode} - {err}");
                        // A 4xx (other than 429) will fail the same way every time: drop it, or it blocks every later message.
                        int code = (int)resp.StatusCode;
                        return (code >= 400 && code < 500) ? SendResult.Drop : SendResult.Retry;
                    }
                }
                // If we reach here, the HTTP post was successful
                Debug.WriteLine($"[Sync] Sent message {message.messageType} (id={message.timeStamp})");
                return SendResult.Sent;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sync] Exception during send: {ex.Message}");
                return SendResult.Retry;
            }
        }

        public static void SetSyncMessageHandler(Action<string, string, string, long> handler)
        {
            _onSyncMessageWithTimestamp = handler;
        }

        private static async Task<HttpResponseMessage> PostWithRetry(HttpContent content, int maxRetries = 3)
        {
            int delayMs = 250;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var response = await _httpClient.PostAsync(AzureFunctionUrl, content);
                    if (response.IsSuccessStatusCode)
                        return response; // SUCCESS

                    Console.WriteLine($"[Sync] Attempt {attempt} failed: {response.StatusCode}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Sync] Retry {attempt} exception: {ex.Message}");
                }

                await Task.Delay(delayMs);
                delayMs *= 2; // exponential backoff
            }

            Console.WriteLine("[Sync] All retries failed — giving up.");
            return null;
        }





        private static string lastSentDoorChangePayload = null;

        public static void MarkLastSentDoorChange(string payload)
        {
            lastSentDoorChangePayload = payload;
        }

        public static bool ShouldSuppressDoorChange(string payload)
        {
            return lastSentDoorChangePayload == payload;
        }

        private static string lastSentRoomChangePayload = null;

        public static void MarkLastSentRoomChange(string payload)
        {
            lastSentRoomChangePayload = payload;
        }

        public static bool ShouldSuppressRoomChange(string payload)
        {
            return lastSentRoomChangePayload == payload;
        }
        private static bool _suppressRoomChange = false;

        public static void BeginSuppressingRoomChanges()
        {
            _suppressRoomChange = true;
        }

        public static void EndSuppressingRoomChanges()
        {
            _suppressRoomChange = false;
        }

        public static bool IsSuppressingRoomChanges()
        {
            return _suppressRoomChange;
        }


        public static void Configure(string azureFunctionUrl)
        {
            _AzureFunctionUrl = SafeNormalizeUrl(azureFunctionUrl);
        }

        public static string AzureFunctionUrl
        {
            get
            {
                return _AzureFunctionUrl;
            }
        }

        public static void SetTileChangeHandler(Action<string, string, string> handler)
        {
            _onTileChange = handler;
        }

        public static void SetSyncMessageHandler(Action<string, string, string> handler)
        {
            _onSyncMessage = handler;
        }

        public static async Task Send(string msgType, string payload, string senderId)
        {
            if (string.IsNullOrEmpty(AzureFunctionUrl))
                throw new InvalidOperationException("AzureFunctionUrl is not set");

            var request = new
            {
                messageType = msgType,
                payload = payload,
                senderId = senderId,
                timeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                //var stopwatch = Stopwatch.StartNew();
                //Console.WriteLine($"[Sync] Sending message {msgType} from {senderId} with payload length {json.Length}");

                var response = await PostWithRetry(content);

                //stopwatch.Stop();
                //Console.WriteLine($"[Sync] Response for {msgType}: {(int)response.StatusCode} in {stopwatch.ElapsedMilliseconds}ms");

                if (!response.IsSuccessStatusCode)
                {
                    var responseText = await response.Content.ReadAsStringAsync();
                    //Console.WriteLine($"[Sync] Response content: {responseText}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sync] ERROR sending message {msgType}: {ex.Message}");
            }
        }






        private static Dictionary<string, string> _pendingMessages = new Dictionary<string, string>();
        private static System.Timers.Timer _debounceTimer;

        public static void SendModelUpdate(string msgType, object model, string senderId)
        {
            var payload = JsonConvert.SerializeObject(model);
            lock (_pendingMessages)
            {
                _pendingMessages[msgType] = payload;
            }

            if (_debounceTimer == null)
            {
                _debounceTimer = new System.Timers.Timer(250); // 250 ms
                _debounceTimer.Elapsed += async (sender, e) =>
                {
                    Dictionary<string, string> toSend;
                    lock (_pendingMessages)
                    {
                        toSend = new Dictionary<string, string>(_pendingMessages);
                        _pendingMessages.Clear();
                    }

                    foreach (var kvp in toSend)
                    {
                        await Send(kvp.Key, kvp.Value, senderId);
                    }
                };
                _debounceTimer.AutoReset = false;
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }




        // one connection start at a time: two overlapping starts (co-op switched on twice quickly) would dispose each other's connection
        private static readonly System.Threading.SemaphoreSlim _startLock = new System.Threading.SemaphoreSlim(1, 1);
        // Reconnecting. WithAutomaticReconnect gives up after about 40 seconds, and a failed first connect was never retried,
        // so after a longer outage this tracker stopped receiving while its sends still went out. Now it keeps trying until co-op is turned off.
        private static volatile bool _wantConnected = false;
        private static string _negotiateUrl;
        private static int _reconnectLoopRunning = 0;

        /// Called (on a pool thread) after each successful connect or reconnect, so the app can resync with the partner.
        public static Action OnConnected;

        public static async Task StartAsync(string negotiateUrl)
        {
            _wantConnected = true;
            _negotiateUrl = negotiateUrl;
            await _startLock.WaitAsync();
            try { await StartAsyncCore(negotiateUrl); }
            catch
            {
                StartReconnectLoop();
                throw;
            }
            finally { _startLock.Release(); }
            RaiseConnected();
        }

        /// Co-op turned off: close the connection and stop reconnecting.
        public static async Task StopAsync()
        {
            _wantConnected = false;
            await _startLock.WaitAsync();
            try
            {
                var old = _connection;
                _connection = null;
                if (old != null)
                {
                    try { await old.DisposeAsync(); } catch (Exception) { }
                }
            }
            finally { _startLock.Release(); }
        }

        private static void RaiseConnected()
        {
            try { OnConnected?.Invoke(); }
            catch (Exception ex) { Console.WriteLine($"[SyncManager] OnConnected failed: {ex.Message}"); }
        }

        private static void StartReconnectLoop()
        {
            if (!_wantConnected) return;
            if (System.Threading.Interlocked.Exchange(ref _reconnectLoopRunning, 1) == 1) return;   // one loop at a time
            Task.Run(async () =>
            {
                try
                {
                    int delayMs = 2000;
                    while (_wantConnected && (_connection == null || _connection.State != HubConnectionState.Connected))
                    {
                        await Task.Delay(delayMs);
                        if (!_wantConnected) break;
                        try
                        {
                            await StartAsync(_negotiateUrl);
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SyncManager] Reconnect failed: {ex.Message}");
                        }
                        delayMs = Math.Min(delayMs * 2, 30000);
                    }
                }
                finally { System.Threading.Interlocked.Exchange(ref _reconnectLoopRunning, 0); }
            });
        }
        private static async Task StartAsyncCore(string negotiateUrl)
        {
            const int maxAttempts = 2;
            const int retryDelayMs = 5000;

            Exception lastException = null;

            negotiateUrl = SafeNormalizeUrl(negotiateUrl);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    Console.WriteLine($"[SyncManager] Attempt {attempt} to start SignalR connection...");

                    // Step 1: Get SignalR connection info
                    var response = await _httpClient.GetAsync(negotiateUrl);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var info = JsonConvert.DeserializeObject<SignalRNegotiation>(json);

                    // Step 2: Build connection. Close any earlier one first: re-enabling co-op used to leave it live, so every message arrived twice.
                    // (cleared before the dispose, so the old connection's Closed handler sees it is no longer current and does not reconnect)
                    var old = _connection;
                    _connection = null;
                    if (old != null)
                    {
                        try { await old.DisposeAsync(); } catch (Exception) { }
                    }
                    var conn = new HubConnectionBuilder()
                        .WithUrl(info.url, options =>
                        {
                            options.AccessTokenProvider = () => Task.FromResult(info.accessToken);
                        })
                        .WithAutomaticReconnect()
                        .Build();
                    _connection = conn;
                    conn.Closed += ex =>
                    {
                        // automatic reconnect gave up (or the host went away): keep trying while co-op is on
                        if (ReferenceEquals(conn, _connection) && _wantConnected)
                        {
                            Console.WriteLine($"[SyncManager] Connection closed: {ex?.Message}");
                            StartReconnectLoop();
                        }
                        return Task.CompletedTask;
                    };
                    conn.Reconnected += _ =>
                    {
                        RaiseConnected();   // messages sent while we were away were missed: resync
                        return Task.CompletedTask;
                    };

                    // Step 3: Register handlers
                    _connection.On<object>("ReceiveMapUpdate", payload =>
                    {
                        try
                        {
                            var jsonPayload = payload.ToString();
                            var update = JsonConvert.DeserializeObject<MapUpdate>(jsonPayload);
                            _onTileChange?.Invoke(update.tileId, update.iconId, update.senderId);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SyncManager] Failed to parse tile update: {ex.Message}");
                        }
                    });

                    _connection.On<SyncMessage>("ReceiveSyncMessage", message =>
                    {
                        try
                        {
                            var payloadJson = message.payload.ToString();
                            long timestamp = message.timeStamp;
                            _onSyncMessageWithTimestamp?.Invoke(message.messageType, payloadJson, message.senderId, timestamp);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[SyncManager] Failed to process sync message: {ex.Message}");
                        }
                    });

                    // Step 4: Start connection
                    await _connection.StartAsync();
                    Console.WriteLine("[SyncManager] Connected to SignalR hub");
                    return; // SUCCESS
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SyncManager] Connection attempt {attempt} failed: {ex.Message}");

                    lastException = ex;
                    if (attempt < maxAttempts)
                        await Task.Delay(retryDelayMs);
                }
            }

            // Final failure
            throw new InvalidOperationException($"[SyncManager] All attempts to start connection failed", lastException);
        }


        // === Supporting Data Structures ===

        private class SignalRNegotiation
        {
            public string url { get; set; }
            public string accessToken { get; set; }
        }

        private class MapUpdate
        {
            public string tileId { get; set; }
            public string iconId { get; set; }
            public string senderId { get; set; }
        }

        public class SyncMessage
        {
            public string messageType { get; set; }
            public string senderId { get; set; }
            public object payload { get; set; }

            public long timeStamp { get; set; }
        }
    }
}