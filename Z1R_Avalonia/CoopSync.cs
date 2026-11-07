// Ported from Z1R_WPF/CoopSync.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
//
// Co-op uses Gob's Z1R_Sync (SyncManager, RoomSyncBridge, CDungeonRoomState) unchanged, through Z1R_Avalonia_Sync, and Newtonsoft.Json as WPF does,
// so the JSON on the wire is the same as the WPF tracker's (see DoorStateAsUnionCaseConverter for the one place a C# type differs).
// F# records → C# records with init properties (property order kept, so the JSON matches); the anonymous record `{| Case: string |}` → DoorStateCase.
// F# async → C#:
// - the `send…` functions whose async body never awaits are plain methods returning a completed Task (F#: `Async<unit>`, always started at once by their callers);
// - `Async.StartImmediate(loop())` polling loops are `async Task` methods started on the calling (UI) thread; `await Task.Delay` resumes on that thread,
//   as F#'s `Async.Sleep` returns to the captured SynchronizationContext;
// - `Async.Start` (thread pool) → `Task.Run`.
// Curried functions take normal parameters. F# model events keep their F# type and are subscribed with `onEvent` (an FSharpHandler).
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.FSharp.Control;
using Microsoft.FSharp.Core;
using Newtonsoft.Json;
using Z1R_Sync;

namespace Z1R_Avalonia;

public static class CoopSync
{
    private static void onEvent<T>(IEvent<FSharpHandler<T>, T> e, Action<T> f) => e.AddHandler(new FSharpHandler<T>((_, x) => f(x)));

    public static string lastSentPlayerProgressJson = "";
    public static string lastSentStartingItemsJson = "";
    public static string lastSentItemsJson = "";
    public static string lastSentOverworldJson = "";
    public static string lastSentDungeonJson = "";
    public static string lastReceivedDungeonJson = "";
    public static string lastSentTriforceJson = "";
    public static string lastReceivedItemsHash = null;   // (F#: string option; null for None)
    public static ConcurrentDictionary<string, string> lastReceivedHashes = new ConcurrentDictionary<string, string>();
    public static ConcurrentDictionary<string, string> lastAppliedHashes = new ConcurrentDictionary<string, string>();
    public static ConcurrentDictionary<string, string> lastSentHashes = new ConcurrentDictionary<string, string>();
    public static string lastSentBlockersJson = "";
    public static bool isDoorSyncReady() =>
        TrackerModelOptions.CoopSyncOptions.GetEnableCoop() &&
        FSharpOption<TrackerModel.DungeonTrackerInstance>.get_IsSome(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption);

    public static readonly SyncOriginTracker dungeonMapsSyncOrigin = new SyncOriginTracker();
    private static readonly int hashRetentionLimit = 20;
    private static readonly ConcurrentDictionary<string, List<string>> recentReceivedHashes = new ConcurrentDictionary<string, List<string>>();

    private static readonly int MAX_ENTRIES = 500;
    private static readonly ConcurrentDictionary<string, string> lastAppliedRoomPayloads = new ConcurrentDictionary<string, string>();
    private static readonly ConcurrentDictionary<string, string> lastAppliedDoorPayloads = new ConcurrentDictionary<string, string>();
    private static readonly ConcurrentDictionary<string, long> lastTimestamps = new ConcurrentDictionary<string, long>();

    private static void pruneIfNeeded<T>(ConcurrentDictionary<string, T> dict, string name)
    {
        if (dict.Count > MAX_ENTRIES)
        {
            var oldestKeys =
                lastTimestamps
                    .OrderBy(kvp => kvp.Value)
                    .Select(kvp => kvp.Key)
                    .Where(dict.ContainsKey)
                    .Take(dict.Count - MAX_ENTRIES)
                    .ToList();

            foreach (var key in oldestKeys)
            {
                var removed = dict.TryRemove(key, out _);
                if (removed)
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Pruned stale entry {key} from {name}");
            }
        }
    }

    public static readonly ConcurrentQueue<DateTime> syncBurstTimestamps = new ConcurrentQueue<DateTime>();
    public static readonly int syncBurstWindowMs = 2000;
    public static readonly int syncBurstLimit = 5;
    public static readonly int syncPauseDurationMs = 1000;
    public static DateTime autoMutedUntil = DateTime.MinValue;

    public sealed record RoomChangePayload
    {
        public int Level { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public bool IsComplete { get; init; }
        public string RoomType { get; init; }
        public string MonsterDetail { get; init; }
        public string FloorDropDetail { get; init; }
        public bool FloorDropAppearsBright { get; init; }
    }

    public sealed record OverworldTileUpdate
    {
        public int X { get; init; }
        public int Y { get; init; }
        public int MapTileValue { get; init; }
        public int ExtraData { get; init; }
        public int CircleValue { get; init; }
    }


    public static string serializeRoomChange(int level, int x, int y, Z1R_Tracker.Models.CDungeonRoomState room)
    {
        var payload = new RoomChangePayload
        {
            Level = level,
            X = x,
            Y = y,
            IsComplete = room.IsComplete,
            RoomType = room.RoomType.ToString(),
            MonsterDetail = room.MonsterDetail.ToString(),
            FloorDropDetail = room.FloorDropDetail.ToString(),
            FloorDropAppearsBright = room.FloorDropAppearsBright
        };
        return JsonConvert.SerializeObject(payload);
    }



    // (F#: the anonymous record `{| Case: string |}`, which is how Newtonsoft writes an F# union case such as Dungeon.DoorState.YES)
    public sealed record DoorStateCase
    {
        public string Case { get; init; }
    }
    public sealed record DoorChangePayload
    {
        public int Level { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public bool IsHorizontal { get; init; }
        public DoorStateCase NewState { get; init; } // will contain "UNKNOWN", "NO", "YES", etc.
    }

    // (port: WPF's Dungeon.DoorState is an F# union, which Newtonsoft writes as {"Case":"YES"}; the port's is a C# enum, which it would write as 2.
    // DoorChangeInfo is serialized with this converter so the DoorChange JSON stays the same as WPF's, and WPF and Avalonia trackers can co-op together.)
    public sealed class DoorStateAsUnionCaseConverter : JsonConverter<Dungeon.DoorState>
    {
        public override void WriteJson(JsonWriter writer, Dungeon.DoorState value, JsonSerializer serializer)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Case");
            writer.WriteValue(value.ToString());
            writer.WriteEndObject();
        }
        public override Dungeon.DoorState ReadJson(JsonReader reader, Type objectType, Dungeon.DoorState existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var c = serializer.Deserialize<DoorStateCase>(reader);
            return Enum.Parse<Dungeon.DoorState>(c.Case);
        }
    }



    public static string computeHash(string s)
    {
        using (var sha = SHA256.Create())
        {
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(s)));
        }
    }

    public static bool alreadyReceived(string messageType, string payloadJson)
    {
        var hash = computeHash(payloadJson);

        var queue =
            recentReceivedHashes.GetOrAdd(messageType, _ => new List<string>());

        lock (queue)
        {
            if (queue.Contains(hash))
                return true;
            else
            {
                queue.Add(hash);
                // Trim oldest if over limit
                if (queue.Count > hashRetentionLimit)
                    queue.RemoveAt(0);
                return false;
            }
        }
    }

    public static bool shouldSend(string messageType, string payloadJson)
    {
        var newHash = computeHash(payloadJson);
        if (lastSentHashes.TryGetValue(messageType, out var oldHash) && oldHash == newHash)
            return false;  // same as last time, skip it
        else
        {
            lastSentHashes[messageType] = newHash;
            return true;
        }
    }

    //let lastAppliedHashes = System.Collections.Concurrent.ConcurrentDictionary<string, string>()
    //let lastTimestamps = System.Collections.Concurrent.ConcurrentDictionary<string, int64>()

    private static readonly Dictionary<string, long> lastAppliedRoomTimestamps = new Dictionary<string, long>();
    private static readonly Dictionary<string, long> lastAppliedDoorTimestamps = new Dictionary<string, long>();

    // (F#'s `%b` prints true/false in lower case)
    private static string makeRoomKey(int level, int x, int y) => $"room-{level}-{x}-{y}";
    private static string makeDoorKey(int level, int x, int y, bool isH) => $"door-{level}-{x}-{y}-{(isH ? "true" : "false")}";

    private static readonly ConcurrentDictionary<string, long> lastSentTimestamps = new ConcurrentDictionary<string, long>();

    /// Get the current timestamp in ms
    private static long now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    /// Record a send event for the given key
    public static void recordSend(string key) =>
        lastSentTimestamps[key] = now();

    /// Should a remote update be ignored due to recent local send?
    public static bool isRecentlySentByUs(string key, int maxAgeMs)
    {
        if (lastSentTimestamps.TryGetValue(key, out var ts))
            return now() - ts < (long)maxAgeMs;
        else
            return false;
    }

    /// Optional cleanup
    public static void purgeOldEntries(int maxAgeMs)
    {
        var threshold = now() - (long)maxAgeMs;
        foreach (var kvp in lastSentTimestamps)
        {
            if (kvp.Value < threshold)
                lastSentTimestamps.TryRemove(kvp.Key, out _);
        }
    }


    public static void recordRoomTimestamp(int level, int x, int y, long timestamp) =>
        lastAppliedRoomTimestamps[makeRoomKey(level, x, y)] = timestamp;

    public static void recordDoorTimestamp(int level, int x, int y, bool isH, long timestamp) =>
        lastAppliedDoorTimestamps[makeDoorKey(level, x, y, isH)] = timestamp;

    public static bool isRoomChangeFresh(int level, int x, int y, long incomingTs)
    {
        if (lastAppliedRoomTimestamps.TryGetValue(makeRoomKey(level, x, y), out var ts))
            return incomingTs > ts;
        else
            return true;
    }

    public static bool isDoorChangeFresh(int level, int x, int y, bool isH, long incomingTs)
    {
        if (lastAppliedDoorTimestamps.TryGetValue(makeDoorKey(level, x, y, isH), out var ts))
            return incomingTs > ts;
        else
            return true;
    }

    public static bool shouldApplyUpdate(string messageType, string payloadJson, long timestamp)
    {
        var newHash = computeHash(payloadJson);

        void updateHashAndTimestamp(string key)
        {
            lastAppliedHashes[messageType] = newHash;
            lastTimestamps[key] = timestamp;
        }

        switch (messageType)
        {
            case "RoomChange":
                try
                {
                    var r = JsonConvert.DeserializeObject<RoomChangePayload>(payloadJson);
                    var key = $"room-{r.Level}-{r.X}-{r.Y}";
                    if (lastAppliedRoomPayloads.TryGetValue(key, out var oldPayload) && oldPayload == payloadJson)
                    {
                        TrackerModelOptions.DebugConfig.Log($"[Sync] Skipped RoomChange — same payload for {key}");
                        return false;
                    }
                    else
                    {
                        if (lastTimestamps.TryGetValue(key, out var prevTs) && timestamp <= prevTs)
                        {
                            TrackerModelOptions.DebugConfig.Log($"[Sync] Skipped RoomChange — stale timestamp ({timestamp} <= {prevTs})");
                            return false;
                        }
                        else
                        {
                            lastAppliedRoomPayloads[key] = payloadJson;
                            updateHashAndTimestamp(key);
                            pruneIfNeeded(lastAppliedRoomPayloads, "RoomPayloads");
                            return true;
                        }
                    }
                }
                catch
                {
                    TrackerModelOptions.DebugConfig.Log("[Sync] RoomChange payload malformed — applying anyway");
                    return true;
                }

            case "DoorChange":
                try
                {
                    var d = JsonConvert.DeserializeObject<DoorChangePayload>(payloadJson);
                    var key = $"door-{d.Level}-{d.X}-{d.Y}-{(d.IsHorizontal ? "true" : "false")}";
                    if (lastAppliedDoorPayloads.TryGetValue(key, out var oldPayload) && oldPayload == payloadJson)
                    {
                        TrackerModelOptions.DebugConfig.Log($"[Sync] Skipped DoorChange — same payload for {key}");
                        return false;
                    }
                    else
                    {
                        if (lastTimestamps.TryGetValue(key, out var prevTs) && timestamp <= prevTs)
                        {
                            TrackerModelOptions.DebugConfig.Log($"[Sync] Skipped DoorChange — stale timestamp ({timestamp} <= {prevTs})");
                            return false;
                        }
                        else
                        {
                            lastAppliedDoorPayloads[key] = payloadJson;
                            updateHashAndTimestamp(key);
                            pruneIfNeeded(lastAppliedDoorPayloads, "DoorPayloads");
                            return true;
                        }
                    }
                }
                catch
                {
                    TrackerModelOptions.DebugConfig.Log("[Sync] DoorChange payload malformed — applying anyway");
                    return true;
                }

            default:
            {
                var key = messageType;
                if (lastAppliedHashes.TryGetValue(messageType, out var oldHash) && oldHash == newHash)
                {
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Skipped {messageType} update — duplicate hash");
                    return false;
                }
                else
                {
                    if (lastTimestamps.TryGetValue(key, out var prevTs) && timestamp <= prevTs)
                    {
                        TrackerModelOptions.DebugConfig.Log($"[Sync] Skipped {messageType} — stale timestamp ({timestamp} <= {prevTs})");
                        return false;
                    }
                    else
                    {
                        updateHashAndTimestamp(key);
                        pruneIfNeeded(lastAppliedHashes, "Hashes");
                        return true;
                    }
                }
            }
        }
    }

    public static bool shouldSendUpdate(string messageType, string payloadJson)
    {
        var now = DateTime.UtcNow;

        if (now < autoMutedUntil)
        {
            TrackerModelOptions.DebugConfig.Log("[Sync] Auto-muted — update skipped");
            return false;
        }
        else
        {
            syncBurstTimestamps.Enqueue(now);

            while (syncBurstTimestamps.TryPeek(out var peekValue) && (now - peekValue).TotalMilliseconds > (double)syncBurstWindowMs)
            {
                syncBurstTimestamps.TryDequeue(out _);
            }

            if (syncBurstTimestamps.Count >= syncBurstLimit)
            {
                autoMutedUntil = now.AddMilliseconds((double)syncPauseDurationMs);
                TrackerModelOptions.DebugConfig.Log("[Sync] Auto-muted due to excessive chatter");
                return false;
            }
            else
                return shouldSend(messageType, payloadJson);
        }
    }

    // (F#: [<AllowNullLiteral>])
    public class DungeonsTriforceState
    {
        public bool[] Triforces { get; set; } = null;
    }


    public static Task sendPlayerProgressUpdate(string myConsoleId)
    {
        try
        {
            var payload = SaveAndLoad.PlayerProgressAndTakeAnyHeartsModel.Create();
            var jsonPayload = JsonConvert.SerializeObject(payload);
            if (jsonPayload != lastSentPlayerProgressJson)
            {
                lastSentPlayerProgressJson = jsonPayload;
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending PlayerProgress update: {jsonPayload}");
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                    SyncManager.EnqueueSync("PlayerProgress", jsonPayload, myConsoleId);
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending PlayerProgress update");
            }
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send PlayerProgress update: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public static bool isApplyingPlayerProgressFromSync = false;

    public static void subscribeToPlayerProgressChanges(string myConsoleId)
    {
        var lastChanged = new TrackerModel.LastChangedTime();

        void markChanged()
        {
            if (!isApplyingPlayerProgressFromSync)
                lastChanged.SetNow();
        }

        // Listen to the TakeAnyHeart array changes
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.TakeAnyHeartChanged, _ => markChanged());

        // Listen to all BoolProperties
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Changed, _ => markChanged());
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Changed, _ => markChanged());
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow.Changed, _ => markChanged());
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing.Changed, _ => markChanged());
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Changed, _ => markChanged());
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Changed, _ => markChanged());
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasDefeatedGanon.Changed, _ => markChanged());
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Changed, _ => markChanged());
        onEvent(TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Changed, _ => markChanged());

        var lastSentTime = DateTime.MinValue;

        async Task loop()
        {
            while (true)
            {
                await Task.Delay(500);
                var currentTime = lastChanged.Time;
                if (currentTime > lastSentTime)
                {
                    lastSentTime = currentTime;
                    await sendPlayerProgressUpdate(myConsoleId);
                }
            }
        }

        CustomComboBoxes.StartImmediate(loop());
    }

    public static Task sendStartingItemsAndExtrasUpdate(string myConsoleId)
    {
        try
        {
            var payload = SaveAndLoad.StartingItemsAndExtrasModel.Create();
            var jsonPayload = JsonConvert.SerializeObject(payload);
            if (jsonPayload != lastSentStartingItemsJson)
            {
                lastSentStartingItemsJson = jsonPayload;
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending StartingItemsAndExtras update: {jsonPayload}");
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                    SyncManager.EnqueueSync("StartingItems", jsonPayload, myConsoleId);
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending StartingItemsAndExtras update");
            }
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send StartingItemsAndExtras update: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public static void subscribeToStartingItemsAndExtrasChanges(string myConsoleId)
    {
        void sendUpdate()
        {
            async Task run()
            {
                await sendStartingItemsAndExtrasUpdate(myConsoleId);
            }
            CustomComboBoxes.StartImmediate(run());
        }

        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasSilverArrow.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasBow.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasWand.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasRedCandle.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasBoomerang.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasMagicBoomerang.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasRedRing.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasPowerBracelet.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasLadder.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasRaft.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasRecorder.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasAnyKey.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasBook.Changed, _ => sendUpdate());
    }

    public static Task sendBlockersUpdate(string myConsoleId)
    {
        try
        {
            var payload =
                Enumerable.Range(0, 8).Select(i =>
                    Enumerable.Range(0, TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON).Select(j =>
                    {
                        var jsonStr = TrackerModel.DungeonBlockersContainer.AsJsonString(i, j);
                        return JsonConvert.DeserializeObject<SaveAndLoad.Blocker>(jsonStr);
                    }).ToArray()
                ).ToArray();
            var jsonPayload = JsonConvert.SerializeObject(payload);
            if (shouldSend("Blockers", jsonPayload))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending Blockers update: {jsonPayload}");
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                    SyncManager.EnqueueSync("Blockers", jsonPayload, myConsoleId);
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending Blockers update");
            }
            else
                TrackerModelOptions.DebugConfig.Log("[Sync] Skipped sending Blockers update — no change");
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send Blockers update: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public static void subscribeToBlockersChanges(string myConsoleId)
    {
        onEvent(TrackerModel.DungeonBlockersContainer.AnyBlockerChanged, _ =>
        {
            async Task run()
            {
                await sendBlockersUpdate(myConsoleId);
            }
            CustomComboBoxes.StartImmediate(run());
        });
    }



    public static SaveAndLoad.Box toSerializableBox(TrackerModel.Box b)
    {
        var box = new SaveAndLoad.Box();
        box.CellCurrent = b.CellCurrent();
        box.PlayerHas = b.PlayerHas().AsInt();
        return box;
    }






    public static Task sendItemsUpdate(string myConsoleId)
    {
        try
        {
            var model = new SaveAndLoad.Items();
            model.HiddenDungeonNumbers = TrackerModel.IsHiddenDungeonNumbers.Invoke(null);
            model.SecondQuestDungeons = TrackerModel.IsSecondQuestDungeons;
            model.WhiteSwordBox = toSerializableBox(TrackerModel.sword2Box);
            model.LadderBox = toSerializableBox(TrackerModel.ladderBox);
            model.ArmosBox = toSerializableBox(TrackerModel.armosBox);

            model.Dungeons =
                Enumerable.Range(0, 9).Select(i =>
                {
                    var d = new SaveAndLoad.Dungeon();
                    var trackerDungeon = TrackerModel.GetDungeon(i);

                    d.Triforce = false; // EXCLUDE this value — syncing Triforce separately
                    d.Color = trackerDungeon.Color;
                    d.LabelChar = trackerDungeon.LabelChar.ToString();
                    d.PlayerHasMap = trackerDungeon.PlayerHasMapOfThisDungeon;
                    d.Boxes =
                        trackerDungeon.Boxes.Select(box => toSerializableBox(box)).ToArray();
                    return d;
                }).ToArray();

            var jsonPayload = JsonConvert.SerializeObject(model);

            if (shouldSend("Items", jsonPayload))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending Items update (excluding Triforce): {jsonPayload}");
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                    SyncManager.EnqueueSync("Items", jsonPayload, myConsoleId);
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending Items update");
            }
            else
                TrackerModelOptions.DebugConfig.Log("[Sync] Skipped sending Items update — no change");
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send Items update: {ex.Message}");
        }
        return Task.CompletedTask;
    }

    public static bool isApplyingItemsFromSync = false;

    public static void subscribeToItemsChanges(string myConsoleId)
    {
        // Shared timestamp that all item-related changes update
        var lastChanged = new TrackerModel.LastChangedTime();

        // Helper to mark the model as changed — suppressed during remote apply to prevent echo
        void markChanged()
        {
            if (!isApplyingItemsFromSync)
                lastChanged.SetNow();
        }

        // Top-level boxes
        onEvent(TrackerModel.sword2Box.Changed, _ => markChanged());
        onEvent(TrackerModel.ladderBox.Changed, _ => markChanged());
        onEvent(TrackerModel.armosBox.Changed, _ => markChanged());

        // Dungeon boxes
        for (int i = 0; i <= 8; i++)
        {
            var dungeon = TrackerModel.GetDungeon(i);
            foreach (var box in dungeon.Boxes)
                onEvent(box.Changed, _ => markChanged());
        }

        // Polling loop to debounce updates
        var lastSentTime = DateTime.MinValue;

        async Task loop()
        {
            while (true)
            {
                await Task.Delay(500);
                var currentTime = lastChanged.Time;
                if (currentTime > lastSentTime)
                {
                    lastSentTime = currentTime;
                    await sendItemsUpdate(myConsoleId);
                }
            }
        }

        CustomComboBoxes.StartImmediate(loop());
    }

    public static SaveAndLoad.Overworld createSerializableOverworldModel()
    {
        var model = new SaveAndLoad.Overworld();
        model.MirrorOverworld = TrackerModel.MirrorOverworld;
        model.StartIconX = TrackerModel.startIconX;
        model.StartIconY = TrackerModel.startIconY;
        model.CustomWaypointX = TrackerModel.customWaypointX;
        model.CustomWaypointY = TrackerModel.customWaypointY;

        var map = new int[16 * 8 * 3];
        for (int j = 0; j <= 7; j++)
        {
            for (int i = 0; i <= 15; i++)
            {
                var index = (j * 16 + i) * 3;
                var cur = TrackerModel.overworldMapMarks[i, j].Current();
                int ed;
                if (cur >= 0 && cur <= TrackerModel.MapSquareChoiceDomainHelper.DARK_X)
                {
                    try
                    {
                        // Shop tiles (items) all store extra data under the SHOP key regardless of which item is first
                        var key = TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur) ? TrackerModel.MapSquareChoiceDomainHelper.SHOP : cur;
                        ed = TrackerModel.getOverworldMapExtraData(i, j, key);
                    }
                    catch { ed = 0; }
                }
                else
                    ed = 0;
                var circ = TrackerModel.overworldMapCircles[i, j];
                map[index] = cur;
                map[index + 1] = ed;
                map[index + 2] = circ;
            }
        }
        model.Map = map;

        return model;
    }



    //let sendOverworldUpdate (myConsoleId: string) =
    //    async {
    //        try
    //            let model = new SaveAndLoad.Overworld()
    //            model.MirrorOverworld <- TrackerModel.MirrorOverworld
    //            model.StartIconX <- TrackerModel.startIconX
    //            model.StartIconY <- TrackerModel.startIconY
    //            model.CustomWaypointX <- TrackerModel.customWaypointX
    //            model.CustomWaypointY <- TrackerModel.customWaypointY

    //            let mapData = Array.zeroCreate<int> (16 * 8 * 3)

    //            for j = 0 to 7 do
    //                for i = 0 to 15 do
    //                    let idx = (j * 16 + i) * 3
    //                    let mark = TrackerModel.overworldMapMarks.[i,j].Current()
    //                    let extra =
    //                        if mark <> -1 && mark <= TrackerModel.MapSquareChoiceDomainHelper.DARK_X then
    //                            try TrackerModel.getOverworldMapExtraData(i,j,mark)
    //                            with _ -> 0
    //                        else 0
    //                    let circ = TrackerModel.overworldMapCircles.[i,j]
    //                    mapData.[idx] <- mark
    //                    mapData.[idx + 1] <- extra
    //                    mapData.[idx + 2] <- circ

    //            model.Map <- mapData

    //            let json = JsonConvert.SerializeObject(model)
    //            printfn "[Sync] Sending Overworld update: %s" json
    //            do! SyncManager.Send("Overworld", json, myConsoleId) |> Async.AwaitTask
    //        with ex ->
    //            printfn "[Sync] Failed to send Overworld update: %s" ex.Message
    //    }
    public static CancellationTokenSource debounceOverworldSend = null;   // (F#: an option; null for None)
    public static readonly Debouncer overworldDebouncer = new Debouncer(200);

    public static void sendOverworldUpdateDebounced(string myConsoleId)
    {
        overworldDebouncer.Trigger(() =>
        {
            Task.Run(() =>   // (F#: Async.Start)
            {
                try
                {
                    // (once per burst of changes, after the debounce: the snapshot is read on the UI thread that owns the tracker state)
                    var jsonPayload = Dispatcher.UIThread.Invoke(() => JsonConvert.SerializeObject(createSerializableOverworldModel()));
                    if (jsonPayload != lastSentOverworldJson)
                    {
                        lastSentOverworldJson = jsonPayload;
                        TrackerModelOptions.DebugConfig.Log($"[Sync] Sending Overworld update: {jsonPayload}");
                        if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                            SyncManager.EnqueueSync("Overworld", jsonPayload, myConsoleId);
                        else
                            TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending Overworld update");
                    }
                }
                catch (Exception ex)
                {
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send Overworld update: {ex.Message}");
                }
            });
        });
    }



    public static void subscribeToOverworldChanges(string myConsoleId)
    {
        var lastSentTime = DateTime.MinValue;

        async Task loop()
        {
            while (true)
            {
                await Task.Delay(500);  // check every 500ms
                var currentTime = TrackerModel.mapLastChangedTime.Time;
                if (currentTime.Ticks > lastSentTime.Ticks)
                {
                    lastSentTime = currentTime.AddMilliseconds(1.0);
                    sendOverworldUpdateDebounced(myConsoleId);
                }
            }
        }
        CustomComboBoxes.StartImmediate(loop());
    }

    public static void subscribeToOverworldTileChanges(string myConsoleId)
    {
        for (int x0 = 0; x0 <= 15; x0++)
        {
            for (int y0 = 0; y0 <= 7; y0++)
            {
                var x = x0; var y = y0;   // (each closure keeps its own x, y, as in F#)
                TrackerModel.overworldMapMarks[x, y].Changed += (_, _) =>   // (a [<CLIEvent>], so a C# event)
                {
                    Task.Run(() =>   // (F#: Async.Start)
                    {
                        try
                        {
                            if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                            {
                                var cur = TrackerModel.overworldMapMarks[x, y].Current();
                                // Shop tiles store extra data (item2, item3) under SHOP key regardless of which item is first
                                var edKey = TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur) ? TrackerModel.MapSquareChoiceDomainHelper.SHOP : cur;
                                var ed = TrackerModel.getOverworldMapExtraData(x, y, edKey);
                                var circ = TrackerModel.overworldMapCircles[x, y];

                                var update = new OverworldTileUpdate
                                {
                                    X = x,
                                    Y = y,
                                    MapTileValue = cur,
                                    ExtraData = ed,
                                    CircleValue = circ
                                };

                                var jsonPayload = JsonConvert.SerializeObject(update);
                                if (shouldSend("OverworldTile", jsonPayload))
                                {
                                    TrackerModelOptions.DebugConfig.Log($"[Sync] Sending OverworldTile ({x},{y}): {jsonPayload}");
                                    SyncManager.EnqueueSync("OverworldTile", jsonPayload, myConsoleId);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send OverworldTile ({x},{y}): {ex.Message}");
                        }
                    });
                };
            }
        }
    }


    /*let createSerializableDungeonArray () =
        [| for i in 0 .. 8 ->
            let d = TrackerModel.GetDungeon(i)
            let model = SaveAndLoad.Dungeon()
            model.Triforce <- d.PlayerHasTriforce()
            model.Color <- d.Color
            model.LabelChar <- string d.LabelChar
            model.PlayerHasMap <- d.PlayerHasMapOfThisDungeon
            //model.Boxes <- d.Boxes |> Array.map (fun b ->
                //let box = SaveAndLoad.Box()
                //box.CellCurrent <- b.CellCurrent()
                //box.PlayerHas <- b.PlayerHas().AsInt()
                //box)
            model
        |]

    let sendDungeonUpdate (myConsoleId: string) =
        async {
            try
                let modelArray = createSerializableDungeonArray()
                let json = JsonConvert.SerializeObject(modelArray)
                if json = lastSentDungeonJson then
                    printfn "[Sync] Skipped sending Dungeon update — no change"
                else
                    lastSentDungeonJson <- json
                    printfn "[Sync] Sending Dungeon update: %s" json
                    do! SyncManager.Send("Dungeon", json, myConsoleId) |> Async.AwaitTask
            with ex ->
                printfn "[Sync] Failed to send Dungeon update: %s" ex.Message
        }

    let subscribeToDungeonChanges (myConsoleId: string) =
        let sendUpdate () =
            async {
                do! sendDungeonUpdate myConsoleId
            } |> Async.StartImmediate

        for i = 0 to 8 do
            let dungeon = TrackerModel.GetDungeon(i)
            for box in dungeon.Boxes do
                box.Changed.Add(fun _ -> sendUpdate())*/


    public static Task sendDungeonTriforceUpdate(string myConsoleId)
    {
        try
        {
            var model = new DungeonsTriforceState();
            model.Triforces = Enumerable.Range(0, 9).Select(i => TrackerModel.GetDungeon(i).PlayerHasTriforce()).ToArray();
            var json = JsonConvert.SerializeObject(model);
            if (shouldSend("DungeonTriforce", json))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending DungeonTriforce update: {json}");
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                    SyncManager.EnqueueSync("DungeonTriforce", json, myConsoleId);
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending DungeonTriforce update");
            }
            else
                TrackerModelOptions.DebugConfig.Log("[Sync] Skipped sending DungeonTriforce update — no change");
        }
        catch (Exception ex)
        {
            Console.Write($"[Sync] Failed to send DungeonTriforce update: {ex.Message}");   // (F#: printf, no newline)
        }
        return Task.CompletedTask;
    }

    public static bool isApplyingTriforceFromSync = false;

    public static void subscribeToDungeonTriforceChanges(string myConsoleId)
    {
        for (int i = 0; i <= 8; i++)
        {
            onEvent(TrackerModel.GetDungeon(i).PlayerHasTriforceChanged, _ =>
            {
                if (!isApplyingTriforceFromSync && TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                    CustomComboBoxes.StartImmediate(sendDungeonTriforceUpdate(myConsoleId));
            });
        }
    }

    public static int[][] transposeHorizontalDoors(int[][] input)
    {
        if (input == null)
            return new int[0][];
        else
            return Enumerable.Range(0, 8).Select(i => Enumerable.Range(0, 7).Select(j => input[j][i]).ToArray()).ToArray();
    }

    public static int[][] transposeVerticalDoors(int[][] input)
    {
        if (input == null)
            return new int[0][];
        else
            return Enumerable.Range(0, 7).Select(i => Enumerable.Range(0, 8).Select(j => input[j][i]).ToArray()).ToArray();
    }

    public static DungeonSaveAndLoad.DungeonRoomModel[][] transposeRoomStates(DungeonSaveAndLoad.DungeonRoomModel[][] input)
    {
        if (input == null)
            return new DungeonSaveAndLoad.DungeonRoomModel[0][];
        else
            return Enumerable.Range(0, 8).Select(i => Enumerable.Range(0, 8).Select(j => input[j][i]).ToArray()).ToArray();
    }

    public static bool[][] transposeRoomIsCircled(bool[][] input)
    {
        if (input == null) return new bool[0][];
        else return Enumerable.Range(0, 8).Select(i => Enumerable.Range(0, 8).Select(j => input[j][i]).ToArray()).ToArray();
    }

    public static DungeonSaveAndLoad.DungeonModel cloneAndFixDungeonModel(DungeonSaveAndLoad.DungeonModel dm)
    {
        var newDM = new DungeonSaveAndLoad.DungeonModel();
        newDM.HorizontalDoors = transposeHorizontalDoors(dm.HorizontalDoors);
        newDM.VerticalDoors = transposeVerticalDoors(dm.VerticalDoors);
        // importFunctions reads RoomIsCircled as [j][i], so pre-transpose it to match (same as RoomStates)
        newDM.RoomIsCircled = transposeRoomIsCircled(dm.RoomIsCircled);
        newDM.RoomStates = transposeRoomStates(dm.RoomStates);
        newDM.VanillaMapOverlay = dm.VanillaMapOverlay;
        return newDM;
    }

    // Create a mutable to debounce updates
    public static string lastDungeonMapsPayload = "";
    public static readonly Debouncer dungeonMapsDebouncer = new Debouncer(200);
    public static string lastSentDungeonMapsPayload = "";




    public static void sendDungeonMapsUpdate(string myConsoleId)
    {
        dungeonMapsDebouncer.Trigger(() =>
        {
            Task.Run(() =>   // (F#: Async.Start)
            {
                if (FSharpOption<TrackerModel.DungeonTrackerInstance>.get_IsNone(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption))
                {
                    TrackerModelOptions.DebugConfig.Log("[Sync] Skipping DungeonMaps update: TrackerModel not yet initialized.");
                    // (F#: `return ()` here, which inside an `async { }` does not leave the block: the code below still runs. Kept as in WPF.)
                }

                try
                {
                    var dungeonModels =
                        Enumerable.Range(0, 9).Select(i =>
                        {
                            var exportFunction = DungeonUI.exportFunctionsLarge[i];
                            var dm = exportFunction();
                            return cloneAndFixDungeonModel(dm);
                        }).ToArray();

                    var jsonPayload = JsonConvert.SerializeObject(dungeonModels, Formatting.None);

                    if (jsonPayload != lastSentDungeonMapsPayload && shouldSendUpdate("DungeonMaps", jsonPayload))
                    {
                        lastSentDungeonMapsPayload = jsonPayload;
                        TrackerModelOptions.DebugConfig.Log($"[Sync] DungeonMaps update: {jsonPayload}");

                        if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                            SyncManager.EnqueueSync("DungeonMaps", jsonPayload, myConsoleId);
                        else
                            TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending DungeonMaps update");
                    }
                    else
                        TrackerModelOptions.DebugConfig.Log("[Sync] Skipped DungeonMaps update — no change");
                }
                catch (Exception ex)
                {
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send DungeonMaps update: {ex.Message}");
                }
            });
        });
    }

    public static DateTime lastSentTime = DateTime.MinValue;

    public static void subscribeToDungeonMapsChanges(string myConsoleId)
    {
        async Task loop()
        {
            while (true)
            {
                await Task.Delay(150);
                var currentChangeTime = TrackerModel.dungeonRoomModelChanged.Time;
                if (currentChangeTime > lastSentTime && !dungeonMapsSyncOrigin.IsApplyingSync)
                {
                    lastSentTime = DateTime.Now;
                    sendDungeonMapsUpdate(myConsoleId);
                }
            }
        }
        CustomComboBoxes.StartImmediate(loop());
    }

    // Create a mutable to debounce updates
    public static string lastHiddenDungeonColorLabelPayload = "";
    public static readonly Debouncer hiddenDungeonColorLabelDebouncer = new Debouncer(200);

    public static void subscribeToHiddenDungeonColorLabelChanges(string myConsoleId)
    {
        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null)
            && TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
        {
            for (int i0 = 0; i0 <= 8; i0++)
            {
                var i = i0;   // (each closure keeps its own i, as in F#)
                var dungeon = TrackerModel.GetDungeon(i);
                onEvent(dungeon.HiddenDungeonColorOrLabelChanged, t =>
                {
                    var (color, labelChar) = t;
                    // (F#: `async { ... } |> Async.StartImmediate`; nothing in it awaits, so it runs right here)
                    try
                    {
                        // (F#: `dict [...]`; Newtonsoft writes the keys in this order)
                        var payload =
                            new Dictionary<string, object>
                            {
                                ["Model"] = "HiddenDungeonColorLabel",
                                ["Index"] = i,
                                ["Color"] = color,
                                ["LabelChar"] = labelChar,
                                ["Source"] = TrackerModelOptions.CoopSyncOptions.MyConsoleId
                            };

                        var jsonPayload = JsonConvert.SerializeObject(payload);

                        if (shouldSend("HiddenDungeonColorLabel", jsonPayload))
                        {
                            hiddenDungeonColorLabelDebouncer.Trigger(() =>
                            {
                                Task.Run(() =>   // (F#: Async.Start)
                                {
                                    if (jsonPayload != lastHiddenDungeonColorLabelPayload)
                                    {
                                        lastHiddenDungeonColorLabelPayload = jsonPayload;
                                        SyncManager.EnqueueSync("HiddenDungeonColorLabel", jsonPayload, myConsoleId);
                                    }
                                });
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send HiddenDungeonColorLabel update: {ex.Message}");
                    }
                });
            }
        }
    }
    private static readonly ConcurrentDictionary<string, Debouncer> roomChangeDebouncers = new ConcurrentDictionary<string, Debouncer>();
    private static readonly ConcurrentDictionary<string, Debouncer> doorChangeDebouncers = new ConcurrentDictionary<string, Debouncer>();

    private static string roomKey(int level, int x, int y) =>
        $"room-{level}-{x}-{y}";

    private static string doorKey(int level, int x, int y, bool isHorizontal) =>
        $"door-{level}-{x}-{y}-{(isHorizontal ? "true" : "false")}";


    // (F#: the body is an `async { }` started with Async.StartImmediate; nothing in it awaits, so it runs right here)
    public static void sendDoorChangeUpdate(DungeonUI.DoorChangeInfo info, string myConsoleId)
    {
        var key = $"DoorChange_{info.Level}_{info.X}_{info.Y}_{(info.IsHorizontal ? "true" : "false")}";
        try
        {
            var jsonPayload = JsonConvert.SerializeObject(info, new DoorStateAsUnionCaseConverter());
            if (shouldSend("DoorChange", jsonPayload))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending DoorChange update: {jsonPayload}");
                SyncManager.MarkLastSentDoorChange(jsonPayload);
                recordSend(key);

                var (doorSyncReady, coopEnabled) =
                    Dispatcher.UIThread.Invoke(() =>
                        (isDoorSyncReady(), TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                    );

                if (doorSyncReady)
                {
                    if (coopEnabled)
                        SyncManager.EnqueueSync("DoorChange", jsonPayload, myConsoleId);
                    else
                        TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending DoorChange update");
                }
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] Door sync not ready, skipping DoorChange update");
            }
            else
                TrackerModelOptions.DebugConfig.Log("[Sync] Skipped sending DoorChange update — no change");
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send DoorChange update: {ex.Message}");
        }
    }

    //)



    public static bool isApplyingRoomFromSync = false;

    public static void subscribeToDoorChanges(string myConsoleId)
    {
        System.Diagnostics.Debug.WriteLine("[Sync] Subscribing to door changes...");
        DungeonUI.doorChangedEvent += info =>
        {
            if (!TrackerModelOptions.CoopSyncOptions.IsBulkDoorInit && !isApplyingRoomFromSync)
                sendDoorChangeUpdate(info, myConsoleId);
        };
    }

    // (F#: the body is an `async { }` started with Async.StartImmediate; nothing in it awaits, so it runs right here)
    public static void sendRoomChangeUpdate(int level, int x, int y, Z1R_Tracker.Models.CDungeonRoomState room, string myConsoleId)
    {
        var key = $"DungeonRoom_{level}_{x}_{y}";
        try
        {
            //let shouldSuppress =
            //    System.Windows.Application.Current.Dispatcher.Invoke(fun () ->
            //        DungeonPopups.suppressRoomSyncTemporarily
            //    )

            //if not shouldSuppress && level <> 0 then
            if (level != 0)
            {
                var json = serializeRoomChange(level, x, y, room);
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop() && shouldSend("RoomChange", json) && !(DungeonPopups.suppressRoomSyncTemporarily))
                {
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Sending RoomChange: {json}");
                    recordSend(key);
                    SyncManager.EnqueueSync("RoomChange", json, myConsoleId);
                }
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] RoomChange not sent — coop disabled or not needed");
            }
            else
                TrackerModelOptions.DebugConfig.Log("[Sync] Room sync suppressed or invalid level — skipping send");
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send RoomChange update: {ex.Message}");
        }
    }

    //)





    public static void subscribeToRoomChanges(string myConsoleId)
    {
        Console.WriteLine("[RoomSync] Starting to subscribe to room changes");

        Z1R_Tracker.Models.RoomSyncBridge.OnRoomChanged =
            new Action<int, int, int, Z1R_Tracker.Models.CDungeonRoomState>((level, x, y, room) =>
            {
                if (!isApplyingRoomFromSync && !(DungeonPopups.suppressRoomSyncTemporarily))
                {
                    // (F#: `Async.StartImmediate(async { ... })`; nothing in it awaits, so it runs right here)
                    try
                    {
                        TrackerModelOptions.DebugConfig.Log($"[RoomSync] OnRoomChanged triggered for L{level} ({x},{y}) - ID {room.DebugId.ToString()}");
                        sendRoomChangeUpdate(level, x, y, room, myConsoleId);
                    }
                    catch (Exception ex)
                    {
                        TrackerModelOptions.DebugConfig.Log($"[RoomSync] ERROR in RoomChanged handler: {ex.Message}");
                    }
                }
            });
    }

    public static void handleRoomChange(string json)
    {
        try
        {
            var data = JsonConvert.DeserializeObject<RoomChangePayload>(json);
            TrackerModelOptions.DebugConfig.Log($"[RoomSync] Received RoomChange for L{data.Level} ({data.X},{data.Y}): {json}");

            if (data.Level < 1 || data.Level > 9 || data.X < 0 || data.X > 7 || data.Y < 0 || data.Y > 7)
                TrackerModelOptions.DebugConfig.Log("[RoomSync] Ignored RoomChange — invalid coordinates");
            else
            {
                Z1R_Tracker.Models.RoomSyncBridge.ApplyRoomChangeFromSync(
                    data.Level, data.X, data.Y,
                    data.IsComplete,
                    data.RoomType,
                    data.MonsterDetail,
                    data.FloorDropDetail,
                    data.FloorDropAppearsBright
                );
            }
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[RoomSync] ERROR handling RoomChange: {ex.Message}");
        }
    }
}
