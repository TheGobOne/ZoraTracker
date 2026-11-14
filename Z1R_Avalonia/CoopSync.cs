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
    public static string lastSentOverworldJson = "";
    public static ConcurrentDictionary<string, string> lastAppliedHashes = new ConcurrentDictionary<string, string>();
    public static ConcurrentDictionary<string, string> lastSentHashes = new ConcurrentDictionary<string, string>();
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
        public string Quest { get; init; }   // the sender's overworld quest (null from older trackers); a tile from another quest is skipped
        public string Extras { get; init; }   // the tile's other state, see tileExtrasOf (null from older trackers: left as it is)
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
            // only an exact repeat of the most recent message of this type is a duplicate: a state that comes back (bombs on, off, on) is a real change.
            // (Matching any of the last N hashes dropped the third message of such a sequence, so the two trackers stayed out of step.)
            if (queue.Count > 0 && queue[queue.Count - 1] == hash)
                return true;
            else
            {
                queue.Add(hash);
                if (queue.Count > hashRetentionLimit)
                    queue.RemoveAt(0);
                return false;
            }
        }
    }

    // Only checks. The hash is recorded by trySend once the message is really queued, so a change made while co-op is off is still sent later.
    public static bool shouldSend(string messageType, string payloadJson)
    {
        var newHash = computeHash(payloadJson);
        if (lastSentHashes.TryGetValue(messageType, out var oldHash) && oldHash == newHash)
            return false;  // same as last time, skip it
        else
            return true;
    }

    // Set while a partner's update of that kind is applied (on the UI thread). The local change handlers do not send while it is set,
    // so a received state is not sent straight back (that echo made two quick changes bounce between the trackers forever).
    public static bool isApplyingOverworldFromSync = false;
    public static bool isApplyingStartingItemsFromSync = false;
    public static bool isApplyingBlockersFromSync = false;

    // Nothing is sent before the run has started and the send caches hold its state (see startupFinished).
    // Before this, a tracker that had just started sent its blank state, which wiped a partner who was mid-run.
    public static volatile bool startupBaselineDone = false;

    // What this tracker knows, for the hello handshake on connect: whether it has any run state, and the time of its latest change.
    private static readonly object stateLock = new object();
    public static bool hasState = false;
    public static long stateTicks = 0;   // UTC ticks

    /// A change the user made here (not one applied from the partner).
    public static void noteLocalEdit()
    {
        if (!startupBaselineDone || TrackerModelOptions.isCurrentlyApplyingRemoteUpdate()) return;
        lock (stateLock) { hasState = true; stateTicks = Math.Max(stateTicks, DateTime.UtcNow.Ticks); }
    }

    private static void noteRemoteState(long ticks)
    {
        lock (stateLock) { hasState = true; if (ticks > stateTicks) stateTicks = ticks; }
    }

    /// Runs a partner's update with currentSyncSource set, so isCurrentlyApplyingRemoteUpdate() is true while it runs. Call it on the UI thread.
    public static void applyRemote(string senderId, long timestamp, Action apply)
    {
        // a tile extras change made here less than a poll ago must be known (edit time, send) before a partner's state can overwrite it
        detectLocalExtrasChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        var saved = TrackerModelOptions.currentSyncSource;
        TrackerModelOptions.currentSyncSource = FSharpOption<string>.Some(senderId ?? "partner");
        try { apply(); }
        finally { TrackerModelOptions.currentSyncSource = saved; }
        noteRemoteState(timestamp);
    }

    // When the user last changed each overworld tile and each item box here (UTC ticks; UI thread only).
    // A full Overworld or Items message built before that edit must not undo it (see tileEditedAfter / boxEditedAfter).
    // Applying the partner's value for a tile or box clears its entry: our edit has then been settled.
    private static readonly long[,] tileEditTicks = new long[16, 8];
    private static readonly Dictionary<object, long> boxEditTicks = new Dictionary<object, long>();   // (keyed by the box object)

    // Times from the two trackers' clocks closer than this count as the same moment (the clocks can differ by a little).
    private static readonly long concurrentTicks = TimeSpan.FromSeconds(5.0).Ticks;

    /// A full Overworld or Items message: keep our value for an entry we changed after the message was built, or up to 5 s before it
    /// (within that time the message may have been built before our change reached the partner). Our state is then sent again.
    /// (No console ID tie-break here: a full message also carries entries the partner never touched, and losing a tie to one of those
    /// would undo our edit on our side only, while the partner keeps it. Edits that really conflict come as OverworldTile messages.)
    public static bool tileEditedAfter(int x, int y, long incomingTicks)
    {
        var ours = tileEditTicks[x, y];
        return ours != 0 && ours > incomingTicks - concurrentTicks;
    }
    public static bool boxEditedAfter(object box, long incomingTicks) =>
        boxEditTicks.TryGetValue(box, out var t) && t > incomingTicks - concurrentTicks;

    /// A partner's OverworldTile message: they changed this very tile. Does our own change of it win?
    /// The later change wins; changes within 5 s of each other are concurrent and the lower console ID wins.
    /// Both trackers decide by the same rule, so they end up with the same value.
    public static bool tileEditWins(int x, int y, long incomingTicks, string senderId)
    {
        var ours = tileEditTicks[x, y];
        if (ours == 0) return false;
        if (Math.Abs(ours - incomingTicks) <= concurrentTicks)
            return string.CompareOrdinal(TrackerModelOptions.CoopSyncOptions.MyConsoleId ?? "", senderId ?? "") < 0;
        return ours > incomingTicks;
    }

    public static void clearTileEdit(int x, int y) => tileEditTicks[x, y] = 0;
    public static void clearBoxEdit(object box) => boxEditTicks.Remove(box);

    // The last DungeonMaps payload applied from the partner (an exact repeat is skipped).
    public static string lastReceivedDungeonMapsJson = "";

    /// Forgets every record of what the partner sent, so the snapshot that follows a hello is applied in full, even parts identical to older messages.
    public static void resetReceivedState()
    {
        recentReceivedHashes.Clear();
        lastAppliedHashes.Clear();
        lastTimestamps.Clear();
        lastAppliedRoomPayloads.Clear();
        lastAppliedDoorPayloads.Clear();
        lastAppliedRoomTimestamps.Clear();
        lastAppliedDoorTimestamps.Clear();
        lastReceivedDungeonMapsJson = "";
        lastReceivedState.Clear();   // (the partner's next whole state is taken as it is)
    }

    // ---- whole-state messages: PlayerProgress, StartingItems, Blockers, DungeonTriforce, Hints ----
    // Each carries all of the sender's flags. Applied as a whole, two changes made at once on the two trackers undid each other
    // (A ticks Bombs, B ticks Wood Sword: each ended with only the other's change). Now only the fields the partner changed since
    // its last message of that kind are applied, our other fields stay, and when our state then differs from theirs we send ours.

    // The last state of each kind received from the partner (UI thread only). No entry: the next one is taken whole (after a hello).
    private static readonly Dictionary<string, string> lastReceivedState = new Dictionary<string, string>();

    /// Our state of this kind, as we would send it. UI thread.
    private static string buildWholeStateJson(string messageType)
    {
        switch (messageType)
        {
            case "PlayerProgress": return buildPlayerProgressJson();
            case "StartingItems": return buildStartingItemsJson();
            case "Blockers": return buildBlockersJson();
            case "DungeonTriforce": return buildTriforceJson();
            case "Hints": return buildHintsJson();
            default: return null;
        }
    }

    /// Records our current state of this kind as what the partner has (after the startup baseline, or after we sent them a full snapshot). UI thread.
    private static void assumePartnerHas(string messageType)
    {
        try { lastReceivedState[messageType] = buildWholeStateJson(messageType); }
        catch (Exception ex) { TrackerModelOptions.DebugConfig.Log($"[Sync] Could not record the {messageType} state: {ex.Message}"); }
    }

    /// A partner's whole state: returns the JSON to apply (their changed fields, our other fields). resendOurs is true when the result
    /// differs from what they sent, so ours must go back to them. UI thread, before the apply.
    public static string mergeWholeState(string messageType, string incomingJson, out bool resendOurs)
    {
        resendOurs = false;
        var incoming = Newtonsoft.Json.Linq.JToken.Parse(incomingJson);
        var had = lastReceivedState.TryGetValue(messageType, out var lastJson) && lastJson != null;
        lastReceivedState[messageType] = incomingJson;
        if (!had) return incomingJson;
        var ours = Newtonsoft.Json.Linq.JToken.Parse(buildWholeStateJson(messageType));
        var keptOurs = false;
        var merged = mergeToken(Newtonsoft.Json.Linq.JToken.Parse(lastJson), incoming, ours, ref keptOurs);
        resendOurs = keptOurs;
        return merged.ToString(Formatting.None);
    }

    // Field by field: a value the partner did not change (same as in their last message) is ours, any other is theirs.
    // keptOurs is set when one of ours that differs from theirs was kept.
    private static Newtonsoft.Json.Linq.JToken mergeToken(Newtonsoft.Json.Linq.JToken last, Newtonsoft.Json.Linq.JToken incoming, Newtonsoft.Json.Linq.JToken ours, ref bool keptOurs)
    {
        if (ours == null || last == null) return incoming.DeepClone();   // (a field we or their last message lack: theirs)
        if (incoming is Newtonsoft.Json.Linq.JObject io && last is Newtonsoft.Json.Linq.JObject lo && ours is Newtonsoft.Json.Linq.JObject oo)
        {
            var r = (Newtonsoft.Json.Linq.JObject)oo.DeepClone();   // (fields an older tracker does not send stay ours)
            foreach (var p in io.Properties())
                r[p.Name] = mergeToken(lo[p.Name], p.Value, oo[p.Name], ref keptOurs);
            return r;
        }
        if (incoming is Newtonsoft.Json.Linq.JArray ia && last is Newtonsoft.Json.Linq.JArray la && ours is Newtonsoft.Json.Linq.JArray oa && ia.Count == la.Count && ia.Count == oa.Count)
        {
            var r = new Newtonsoft.Json.Linq.JArray();
            for (int k = 0; k < ia.Count; k++)
                r.Add(mergeToken(la[k], ia[k], oa[k], ref keptOurs));
            return r;
        }
        if (!Newtonsoft.Json.Linq.JToken.DeepEquals(incoming, last)) return incoming.DeepClone();   // (the partner changed it)
        if (!Newtonsoft.Json.Linq.JToken.DeepEquals(incoming, ours)) keptOurs = true;
        return ours.DeepClone();
    }

    /// After a merged whole state was applied: ours differs from what the partner sent, so send ours (else record it as sent). UI thread.
    public static void finishWholeState(string messageType, bool resendOurs)
    {
        if (!resendOurs) { rebaseline(messageType); return; }
        var me = TrackerModelOptions.CoopSyncOptions.MyConsoleId;
        switch (messageType)
        {
            case "PlayerProgress": lastSentPlayerProgressJson = ""; sendPlayerProgressUpdate(me); break;
            case "StartingItems": lastSentStartingItemsJson = ""; sendStartingItemsAndExtrasUpdate(me); break;
            case "Blockers": lastSentHashes.TryRemove("Blockers", out _); sendBlockersUpdate(me); break;
            case "DungeonTriforce": lastSentHashes.TryRemove("DungeonTriforce", out _); sendDungeonTriforceUpdate(me); break;
            case "Hints": lastSentHashes.TryRemove("Hints", out _); sendHintsUpdate(); break;
        }
        if (messageType == "Hints") hintsBaseline = buildHintsJson();   // (the merged hints are not a local edit)
    }

    /// Our own change: a partner message identical to one we got before is a real change again, so forget what we received of this kind.
    private static void forgetReceived(string messageType)
    {
        recentReceivedHashes.TryRemove(messageType, out _);
        lastAppliedHashes.TryRemove(messageType, out _);
    }

    /// Co-op is on (or the self-test forces it). The polls skip their work while it is off.
    public static bool coopActive() => testForceCoop || TrackerModelOptions.CoopSyncOptions.GetEnableCoop();

    /// Queues a message if co-op is on. Returns false (and records nothing) when it was not sent.
    private static bool trySend(string messageType, string payloadJson)
    {
        if (!startupBaselineDone)
            return false;
        if (!coopActive())
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Coop sync is disabled, not sending {messageType} update");
            return false;
        }
        SyncManager.EnqueueSync(messageType, payloadJson, TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        lastSentHashes[messageType] = computeHash(payloadJson);
        forgetReceived(messageType);
        return true;
    }

    //let lastAppliedHashes = System.Collections.Concurrent.ConcurrentDictionary<string, string>()
    //let lastTimestamps = System.Collections.Concurrent.ConcurrentDictionary<string, int64>()

    // (concurrent: written on the SignalR thread, cleared on the UI thread by resetReceivedState)
    private static readonly ConcurrentDictionary<string, long> lastAppliedRoomTimestamps = new ConcurrentDictionary<string, long>();
    private static readonly ConcurrentDictionary<string, long> lastAppliedDoorTimestamps = new ConcurrentDictionary<string, long>();

    // (F#'s `%b` prints true/false in lower case)
    private static string makeRoomKey(int level, int x, int y) => $"room-{level}-{x}-{y}";
    private static string makeDoorKey(int level, int x, int y, bool isH) => $"door-{level}-{x}-{y}-{(isH ? "true" : "false")}";

    // When the user last changed each room or door here (UTC ticks), keyed by roomEditKey / doorEditKey. Applying the partner's value removes the entry.
    private static readonly ConcurrentDictionary<string, long> lastSentTicks = new ConcurrentDictionary<string, long>();

    public static string roomEditKey(int level, int x, int y) => $"DungeonRoom_{level}_{x}_{y}";
    public static string doorEditKey(int level, int x, int y, bool isH) => $"DoorChange_{level}_{x}_{y}_{(isH ? "true" : "false")}";

    /// Our own edit of this room or door, now
    public static void recordSend(string key) => lastSentTicks[key] = DateTime.UtcNow.Ticks;

    /// The partner's value of this room or door was taken: our edit of it is settled.
    public static void clearEdit(string key) => lastSentTicks.TryRemove(key, out _);

    /// A partner's RoomChange or DoorChange for a room or door we changed too: does ours win?
    /// As tileEditWins: the later change wins; changes within 5 s of each other are concurrent (the two clocks can differ a little)
    /// and the lower console ID wins. Both trackers decide by the same rule; the winner sends its value again (resendRoom / resendDoor).
    /// (maxAgeMs is not used: the window is concurrentTicks.)
    public static bool localEditWins(string key, int maxAgeMs, long incomingTicks, string senderId)
    {
        if (!lastSentTicks.TryGetValue(key, out var ours))
            return false;
        if (Math.Abs(ours - incomingTicks) <= concurrentTicks)
            return string.CompareOrdinal(TrackerModelOptions.CoopSyncOptions.MyConsoleId ?? "", senderId ?? "") < 0;
        return ours > incomingTicks;
    }

    /// A whole DungeonMaps arrived: keep our value of this room or door when we changed it after the message was built, or up to 5 s before
    /// (as tileEditedAfter). The kept ones are sent again as RoomChange / DoorChange, so the per-room rule settles them on both sides.
    public static bool localEditNewer(string key, long incomingTicks) =>
        lastSentTicks.TryGetValue(key, out var ours) && ours > incomingTicks - concurrentTicks;

    /// Sends our room as it is now, after it won a conflict (not a new edit: its edit time stays). UI thread.
    public static void resendRoom(int level, int x, int y)
    {
        if (level < 1 || level > 9 || x < 0 || x > 7 || y < 0 || y > 7) return;
        var room = DungeonUI.masterRoomStatesPublic?[level - 1][x, y]?.CSharp;
        if (room == null) return;
        lastSentHashes.TryRemove("RoomChange", out _);
        sendRoomChangeUpdate(level, x, y, room, TrackerModelOptions.CoopSyncOptions.MyConsoleId, true);
    }

    /// As resendRoom, for a door. UI thread.
    public static void resendDoor(int level, int x, int y, bool isH)
    {
        if (level < 1 || level > 9 || x < 0 || y < 0 || x > (isH ? 6 : 7) || y > (isH ? 7 : 6)) return;
        var door = isH ? DungeonUI.getHorizontalDoor(level, x, y) : DungeonUI.getVerticalDoor(level, x, y);
        if (door == null) return;
        lastSentHashes.TryRemove("DoorChange", out _);
        sendDoorChangeUpdate(new DungeonUI.DoorChangeInfo { Level = level, X = x, Y = y, IsHorizontal = isH, NewState = door.State }, TrackerModelOptions.CoopSyncOptions.MyConsoleId, true);
    }

    /// A partner's room change was applied: our last sent RoomChange no longer describes our state, so the same change must be sendable again.
    public static void remoteRoomApplied() => lastSentHashes.TryRemove("RoomChange", out _);

    /// As remoteRoomApplied, for doors.
    public static void remoteDoorApplied()
    {
        lastSentHashes.TryRemove("DoorChange", out _);
        SyncManager.MarkLastSentDoorChange(null);
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
        // (autoMutedUntil is read by sendDungeonMapsUpdate to schedule a retry)
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
            var jsonPayload = buildPlayerProgressJson();
            if (jsonPayload != lastSentPlayerProgressJson)
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending PlayerProgress update: {jsonPayload}");
                if (trySend("PlayerProgress", jsonPayload))
                    lastSentPlayerProgressJson = jsonPayload;
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
            if (!isApplyingPlayerProgressFromSync && !TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
            {
                lastChanged.SetNow();
                noteLocalEdit();
            }
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
            var jsonPayload = buildStartingItemsJson();
            if (jsonPayload != lastSentStartingItemsJson)
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending StartingItemsAndExtras update: {jsonPayload}");
                if (trySend("StartingItems", jsonPayload))
                    lastSentStartingItemsJson = jsonPayload;
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
            if (isApplyingStartingItemsFromSync || TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
                return;   // a partner's update being applied: do not send it back
            noteLocalEdit();
            async Task run()
            {
                await sendStartingItemsAndExtrasUpdate(myConsoleId);
            }
            CustomComboBoxes.StartImmediate(run());
        }

        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasSword4.Changed, _ => sendUpdate());
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
            var jsonPayload = buildBlockersJson();
            if (shouldSend("Blockers", jsonPayload))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending Blockers update: {jsonPayload}");
                trySend("Blockers", jsonPayload);
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
            if (isApplyingBlockersFromSync || TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
                return;   // a partner's update being applied: do not send it back
            noteLocalEdit();
            async Task run()
            {
                await sendBlockersUpdate(myConsoleId);
            }
            CustomComboBoxes.StartImmediate(run());
        });
    }

    // ---- hints: the level hints and the "other hints" checkboxes ----
    // (the checkboxes fire no event, so a cheap poll finds the changes, as for the tile extras)

    public static string buildHintsJson()
    {
        var h = new SaveAndLoad.Hints();
        h.LocationHints = Enumerable.Range(0, 11).Select(i => TrackerModel.GetLevelHint.Invoke(i).ToIndex()).ToArray();
        h.NoFeatOfStrengthHint = TrackerModel.NoFeatOfStrengthHintWasGiven;
        h.SailNotHint = TrackerModel.SailNotHintWasGiven;
        h.SailToHint = TrackerModel.SailToHintWasGiven;
        h.PlayMelodyHint = TrackerModel.PlayMelodyHintWasGiven;
        h.StepOverWaterHint = TrackerModel.StepOverWaterHintWasGiven;
        h.FireArrowsHint = TrackerModel.FireArrowsHintWasGiven;
        return JsonConvert.SerializeObject(h);
    }

    private static string hintsBaseline = null;   // the hints as last seen by the poll (a change from it is a local edit)

    /// UI thread.
    public static void sendHintsUpdate()
    {
        try
        {
            var json = buildHintsJson();
            if (shouldSend("Hints", json))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending Hints update: {json}");
                trySend("Hints", json);
            }
        }
        catch (Exception ex) { TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send Hints update: {ex.Message}"); }
    }

    private static bool hintsLoopStarted = false;
    /// Once, after the run has started. UI thread.
    public static void subscribeToHintsChanges()
    {
        if (hintsLoopStarted) return;
        hintsLoopStarted = true;
        hintsBaseline = buildHintsJson();
        async Task loop()
        {
            while (true)
            {
                await Task.Delay(500);
                try
                {
                    var now = buildHintsJson();
                    if (now != hintsBaseline)
                    {
                        hintsBaseline = now;
                        noteLocalEdit();   // (a partner's hints update the baseline themselves)
                    }
                    if (coopActive()) sendHintsUpdate();
                }
                catch (Exception ex) { TrackerModelOptions.DebugConfig.Log($"[Sync] Hints poll failed: {ex.Message}"); }
            }
        }
        CustomComboBoxes.StartImmediate(loop());
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
            var jsonPayload = buildItemsJson();

            if (shouldSend("Items", jsonPayload))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending Items update (excluding Triforce): {jsonPayload}");
                trySend("Items", jsonPayload);
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

    public static string buildPlayerProgressJson() => JsonConvert.SerializeObject(SaveAndLoad.PlayerProgressAndTakeAnyHeartsModel.Create());
    public static string buildStartingItemsJson() => JsonConvert.SerializeObject(SaveAndLoad.StartingItemsAndExtrasModel.Create());
    public static string buildBlockersJson()
    {
        var payload =
            Enumerable.Range(0, 8).Select(i =>
                Enumerable.Range(0, TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON).Select(j =>
                {
                    var jsonStr = TrackerModel.DungeonBlockersContainer.AsJsonString(i, j);
                    return JsonConvert.DeserializeObject<SaveAndLoad.Blocker>(jsonStr);
                }).ToArray()
            ).ToArray();
        return JsonConvert.SerializeObject(payload);
    }
    public static string buildTriforceJson()
    {
        var model = new DungeonsTriforceState();
        model.Triforces = Enumerable.Range(0, 9).Select(i => TrackerModel.GetDungeon(i).PlayerHasTriforce()).ToArray();
        return JsonConvert.SerializeObject(model);
    }
    // The overworld quest goes along with the map, so a partner on a different quest can skip it (the tile values mean other things there).
    // (the quest number as saves write it. The Overworld message used to put the quest's name in "Quest", which SaveAndLoad.Overworld reads
    // as a number, so the receiver failed to read every full Overworld message.)
    public static string currentOverworldQuest() => TrackerModel.owInstance.Quest.AsInt().ToString();
    public static string buildOverworldJson()
    {
        var model = createSerializableOverworldModel();
        model.Quest = TrackerModel.owInstance.Quest.AsInt();
        // the shop windows' stash and cheap key flags travel in the save's own fields
        model.ShopStash = (int[])TrackerModel.shopStash.Clone();
        model.CheapKeyShops = TrackerModel.cheapKeyShops.Select(b => b ? 1 : 0).ToArray();
        var jo = Newtonsoft.Json.Linq.JObject.FromObject(model);
        // each tile's other state (marker icons, bomb dropper, price, ...), row by row like Map; older trackers ignore it
        var extras = new Newtonsoft.Json.Linq.JArray();
        for (int j = 0; j <= 7; j++)
            for (int i = 0; i <= 15; i++)
                extras.Add(tileExtrasOf(i, j));
        jo["TileExtras"] = extras;
        return jo.ToString(Formatting.None);
    }

    // ---- The state of an overworld tile beyond its icon value, extra data and circle ----
    // (it changes without the tile's Changed event, so a poll finds the changes; see subscribeToOverworldExtrasChanges)
    // Packed as "icons|bomb|price|hintColor|shuffledWoodSword|tiers|dups|circle":
    //   icons: owSwordIcons, the marker icons with their collected checks and the SHOP n label; bomb: owBombDroppers (1/0);
    //   price: overworldShopPrices (0 = none); hintColor: hintShopColors (-1 = none); shuffledWoodSword: shuffledWoodSwordTiles (1/0);
    //   tiers: progressiveTileTier for this tile, "item:tier,..."; dups: WPFUI.dupLinks from this tile, "x:y,...";
    //   circle: overworldMapCircles (also in Map, but a circle change fires no event: it is here so the poll finds it; older trackers send 7 fields).
    public static string tileExtrasOf(int i, int j)
    {
        var key = Tuple.Create(i, j);
        var icons = TrackerModel.owSwordIcons[i, j];
        var bomb = TrackerModel.owBombDroppers[i, j] ? 1 : 0;
        var price = TrackerModel.overworldShopPrices[i, j];
        var hint = TrackerModel.hintShopColors.TryGetValue(key, out var h) ? h : -1;
        var shuffled = TrackerModel.shuffledWoodSwordTiles.Contains(key) ? 1 : 0;
        var tiers = string.Join(",", TrackerModel.progressiveTileTier.Where(kv => kv.Key.Item1 == i && kv.Key.Item2 == j)
                                                                  .Select(kv => $"{kv.Key.Item3}:{kv.Value}").OrderBy(t => t, StringComparer.Ordinal));
        var dups = WPFUI.dupLinks.TryGetValue((i, j), out var l) ? string.Join(",", l.Select(d => $"{d.Item1}:{d.Item2}").OrderBy(t => t, StringComparer.Ordinal)) : "";
        return $"{icons}|{bomb}|{price}|{hint}|{shuffled}|{tiers}|{dups}|{TrackerModel.overworldMapCircles[i, j]}";
    }

    private static readonly string[,] tileExtrasBaseline = new string[16, 8];
    private static int[] shopStashBaseline = new int[6];
    private static bool[] cheapKeyBaseline = new bool[6];
    private static string boxTiersBaseline = "";   // (box tiers change without a box event when an overworld item is un-collected: the poll sends them)

    /// Records the current extras of every tile (and the shop stash) as known, so the poll only reports later changes. UI thread.
    public static void rebaselineTileExtras()
    {
        for (int i = 0; i <= 15; i++)
            for (int j = 0; j <= 7; j++)
                tileExtrasBaseline[i, j] = tileExtrasOf(i, j);
        shopStashBaseline = (int[])TrackerModel.shopStash.Clone();
        cheapKeyBaseline = (bool[])TrackerModel.cheapKeyShops.Clone();
    }

    /// Puts a partner's extras on one tile. Returns true when something changed (call finishTileExtras after a batch). UI thread, inside applyRemote.
    public static bool applyTileExtras(int i, int j, string extras)
    {
        if (string.IsNullOrEmpty(extras) || extras == tileExtrasOf(i, j)) return false;
        var p = extras.Split('|');
        if (p.Length < 7) return false;
        int num(string s, int d) => int.TryParse(s, out var v) ? v : d;
        bool onMap(int x, int y) => x >= 0 && x <= 15 && y >= 0 && y <= 7;
        var key = Tuple.Create(i, j);
        TrackerModel.owSwordIcons[i, j] = num(p[0], -1);
        TrackerModel.owBombDroppers[i, j] = p[1] == "1";
        TrackerModel.overworldShopPrices[i, j] = Math.Max(0, num(p[2], 0));
        var hint = num(p[3], -1);
        if (hint >= 0) TrackerModel.hintShopColors[key] = hint; else TrackerModel.hintShopColors.Remove(key);
        if (p[4] == "1") TrackerModel.shuffledWoodSwordTiles.Add(key); else TrackerModel.shuffledWoodSwordTiles.Remove(key);
        foreach (var k in TrackerModel.progressiveTileTier.Keys.Where(k => k.Item1 == i && k.Item2 == j).ToList())
            TrackerModel.progressiveTileTier.Remove(k);
        foreach (var t in p[5].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var q = t.Split(':');
            if (q.Length == 2 && int.TryParse(q[0], out var item) && int.TryParse(q[1], out var tier))
                TrackerModel.progressiveTileTier[Tuple.Create(i, j, item)] = tier;
        }
        var dsts = new List<(int, int)>();
        foreach (var t in p[6].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var q = t.Split(':');
            if (q.Length == 2 && int.TryParse(q[0], out var x) && int.TryParse(q[1], out var y) && onMap(x, y) && (x, y) != (i, j) && !dsts.Contains((x, y)))
                dsts.Add((x, y));
        }
        if (dsts.Count > 0) WPFUI.dupLinks[(i, j)] = dsts; else WPFUI.dupLinks.Remove((i, j));
        if (p.Length >= 8) TrackerModel.overworldMapCircles[i, j] = num(p[7], TrackerModel.overworldMapCircles[i, j]);
        tileExtrasBaseline[i, j] = tileExtrasOf(i, j);   // (a partner's state: the poll must not send it back)
        return true;
    }

    /// The partner's shop stash and cheap key flags, from a full Overworld message (null arrays: older tracker, left alone). Returns true when they changed. UI thread.
    public static bool applyShopGlobals(int[] stash, int[] cheapKeys)
    {
        var changed = false;
        if (stash != null && stash.Length == 6)
            for (int k = 0; k <= 5; k++)
                if (TrackerModel.shopStash[k] != stash[k]) { TrackerModel.shopStash[k] = stash[k]; changed = true; }
        if (cheapKeys != null && cheapKeys.Length == 6)
            for (int k = 0; k <= 5; k++)
                if (TrackerModel.cheapKeyShops[k] != (cheapKeys[k] == 1)) { TrackerModel.cheapKeyShops[k] = cheapKeys[k] == 1; changed = true; }
        shopStashBaseline = (int[])TrackerModel.shopStash.Clone();
        cheapKeyBaseline = (bool[])TrackerModel.cheapKeyShops.Clone();
        if (changed) { try { PieMenus.cheapKeysChangedHook(); } catch (Exception) { } }
        return changed;
    }

    /// After applying extras: recount what is collected on the map and redraw the changed tiles, as loading a save does. UI thread.
    public static void finishTileExtras(List<(int, int)> changed)
    {
        if (changed.Count == 0) return;
        WPFUI.rebuildMapCollected();
        TrackerModel.recomputePlayerStateSummary();
        WPFUI.decorStateChanged();
        foreach (var (x, y) in changed)
            WPFUI.redrawOwTileHook(x, y);
    }

    /// Polls for local changes to the tile extras and the shop stash (none of them fires an event) and sends them:
    /// each changed tile as an OverworldTile message (so the conflict rules apply to it), and the full Overworld message.
    private static bool extrasLoopStarted = false;
    private static bool extrasPollActive = false;   // (co-op was on at the last poll, so the baseline is current)
    public static void subscribeToOverworldExtrasChanges(string myConsoleId)
    {
        if (extrasLoopStarted) return;
        extrasLoopStarted = true;
        rebaselineTileExtras();
        extrasPollActive = coopActive();
        async Task loop()
        {
            while (true)
            {
                await Task.Delay(500);
                detectLocalExtrasChanges(myConsoleId);
            }
        }
        CustomComboBoxes.StartImmediate(loop());
    }

    /// Finds tile extras (and shop stash) changes made here since the last look, records their edit times and sends them. UI thread.
    public static void detectLocalExtrasChanges(string myConsoleId)
    {
        if (!extrasLoopStarted) return;
        // (co-op off: no 128-tile scan. When it comes on, the current state is the new baseline; the hello on connect brings the trackers in step)
        if (!coopActive()) { extrasPollActive = false; return; }
        if (!extrasPollActive)
        {
            extrasPollActive = true;
            rebaselineTileExtras();
            boxTiersBaseline = buildBoxTiers();
            return;
        }
        {
            {
                try
                {
                    var changedAny = false;
                    for (int i = 0; i <= 15; i++)
                        for (int j = 0; j <= 7; j++)
                        {
                            var now = tileExtrasOf(i, j);
                            if (now == tileExtrasBaseline[i, j]) continue;
                            tileExtrasBaseline[i, j] = now;
                            changedAny = true;
                            tileEditTicks[i, j] = DateTime.UtcNow.Ticks;   // (a local change: remote applies update the baseline themselves)
                            sendTile(i, j);
                        }
                    if (!TrackerModel.shopStash.SequenceEqual(shopStashBaseline) || !TrackerModel.cheapKeyShops.SequenceEqual(cheapKeyBaseline))
                    {
                        shopStashBaseline = (int[])TrackerModel.shopStash.Clone();
                        cheapKeyBaseline = (bool[])TrackerModel.cheapKeyShops.Clone();
                        changedAny = true;
                    }
                    var tiersNow = buildBoxTiers();
                    if (tiersNow != boxTiersBaseline)
                    {
                        boxTiersBaseline = tiersNow;
                        sendItemsUpdate(myConsoleId);   // (no noteLocalEdit: the un-collect that moved the tiers was already noted, and a partner's one must not count as ours)
                    }
                    if (changedAny)
                    {
                        noteLocalEdit();
                        sendOverworldUpdateDebounced(myConsoleId);
                    }
                }
                catch (Exception ex)
                {
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Overworld extras poll failed: {ex.Message}");
                }
            }
        }
    }
    public static string buildDungeonMapsJson()
    {
        var dungeonModels = Enumerable.Range(0, 9).Select(i => cloneAndFixDungeonModel(DungeonUI.exportFunctionsLarge[i]())).ToArray();
        return JsonConvert.SerializeObject(dungeonModels, Formatting.None);
    }

    /// After a partner's update of this kind is applied: the current state counts as sent, so it is not sent back,
    /// and the next local change (even back to what we sent before) is sent. Call it on the UI thread.
    public static void rebaseline(string messageType)
    {
        try
        {
            switch (messageType)
            {
                case "PlayerProgress": lastSentPlayerProgressJson = buildPlayerProgressJson(); break;
                case "StartingItems": lastSentStartingItemsJson = buildStartingItemsJson(); break;
                case "Items": lastSentHashes["Items"] = computeHash(buildItemsJson()); boxTiersBaseline = buildBoxTiers(); break;
                case "Blockers": lastSentHashes["Blockers"] = computeHash(buildBlockersJson()); break;
                case "DungeonTriforce": lastSentHashes["DungeonTriforce"] = computeHash(buildTriforceJson()); break;
                case "Overworld": lastSentOverworldJson = buildOverworldJson(); boxTiersBaseline = buildBoxTiers(); break;
                case "Notes": lastSentHashes["Notes"] = computeHash(buildNotesJson()); break;
                case "Hints": hintsBaseline = buildHintsJson(); lastSentHashes["Hints"] = computeHash(hintsBaseline); break;
            }
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Could not record the {messageType} state: {ex.Message}");
        }
    }

    /// Called once when a run has started (new or loaded), on the UI thread. Records the current state as the baseline,
    /// so only changes from now on are sent, then says hello to the partner if connected.
    /// loadedTicks: for a loaded save, the save file's last write time (UTC ticks), used as its state time in the hello.
    public static void startupFinished(bool loadedSave, long loadedTicks = 0)
    {
        foreach (var t in new[] { "PlayerProgress", "StartingItems", "Items", "Blockers", "DungeonTriforce", "Overworld", "Notes", "Hints" })
            rebaseline(t);
        foreach (var t in wholeStateTypes)
            assumePartnerHas(t);   // (until a hello says otherwise, the partner's state is taken to be ours: their first message then merges)
        try
        {
            lastSentDungeonMapsPayload = buildDungeonMapsJson();
            lastSentHashes["DungeonMaps"] = computeHash(lastSentDungeonMapsPayload);
        }
        catch (Exception ex) { TrackerModelOptions.DebugConfig.Log($"[Sync] Could not record the DungeonMaps state: {ex.Message}"); }
        lastSentTime = DateTime.Now;
        lock (stateLock) { hasState = loadedSave; stateTicks = loadedSave ? loadedTicks : 0; }
        startupBaselineDone = true;
        subscribeToOverworldExtrasChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId);   // (after the baseline: the loaded tiles are not sent as changes)
        subscribeToNotesChanges();
        subscribeToHintsChanges();
        startScreenOptionsHandler = null;
        sendStartupOptions();
        sendStateHello(true);   // (a run that just started is joining the session)
    }

    /// Sends everything this tracker has, ignoring what was sent before. Used when the hello handshake picks this tracker's state. UI thread.
    public static void sendFullSnapshot(string myConsoleId)
    {
        if (!startupBaselineDone) return;
        lastSentPlayerProgressJson = "";
        lastSentStartingItemsJson = "";
        lastSentOverworldJson = "";
        lastSentDungeonMapsPayload = "";
        lastSentHashes.Clear();
        sendPlayerProgressUpdate(myConsoleId);
        sendStartingItemsAndExtrasUpdate(myConsoleId);
        sendItemsUpdate(myConsoleId);
        sendBlockersUpdate(myConsoleId);
        sendDungeonTriforceUpdate(myConsoleId);
        sendOverworldUpdateDebounced(myConsoleId);
        sendDungeonMapsUpdate(myConsoleId);
        sendNotesUpdate();
        sendHintsUpdate();
        foreach (var t in wholeStateTypes)
            assumePartnerHas(t);   // (the partner takes this snapshot whole)
    }

    private static readonly string[] wholeStateTypes = { "PlayerProgress", "StartingItems", "Blockers", "DungeonTriforce", "Hints" };

    // The hello handshake, sent when a run starts and after every connect or reconnect (those hellos are "joining").
    // The winner sends a full snapshot and the other one takes it:
    // - a tracker with no state never wins, so it cannot wipe a partner;
    // - a tracker already in the session wins against one that is joining (the joiner missed messages, so its state is the one to replace);
    // - otherwise (both joining, or neither) the newer state time wins, and a tie goes to the lower console ID.
    public sealed class StateHello
    {
        public bool HasState { get; set; }
        public long StateTicks { get; set; }
        public bool Joining { get; set; }
        public string Nonce { get; set; }   // (makes each hello different, so it is never dropped as a duplicate)
    }

    // We count as joining for a short time after our own joining hello, long enough for the handshake to finish.
    private static DateTime joiningUntil = DateTime.MinValue;
    private static bool meJoining() => DateTime.UtcNow < joiningUntil;

    /// joining: true when this tracker has just started a run or (re)connected. UI thread.
    public static void sendStateHello(bool joining = false)
    {
        if (!startupBaselineDone || !TrackerModelOptions.CoopSyncOptions.GetEnableCoop()) return;
        if (joining)
        {
            joiningUntil = DateTime.UtcNow.AddSeconds(15.0);
            resetReceivedState();   // (we will take the partner's snapshot if they win: apply all of it)
        }
        StateHello hello;
        lock (stateLock) hello = new StateHello { HasState = hasState, StateTicks = stateTicks, Joining = meJoining(), Nonce = Guid.NewGuid().ToString() };
        SyncManager.EnqueueSync("StateHello", JsonConvert.SerializeObject(hello), TrackerModelOptions.CoopSyncOptions.MyConsoleId);
    }

    /// A partner's hello. UI thread.
    public static void handleStateHello(StateHello them, string senderId)
    {
        if (!startupBaselineDone) return;   // our run has not started: we say hello ourselves once it has
        bool iWin;
        var joining = meJoining();
        lock (stateLock)
        {
            if (!hasState) iWin = false;
            else if (!them.HasState) iWin = true;
            else if (them.Joining != joining) iWin = them.Joining;   // the one already in the session wins
            else iWin = stateTicks > them.StateTicks
                        || (stateTicks == them.StateTicks && string.CompareOrdinal(TrackerModelOptions.CoopSyncOptions.MyConsoleId ?? "", senderId ?? "") < 0);
        }
        if (iWin)
            sendFullSnapshot(TrackerModelOptions.CoopSyncOptions.MyConsoleId);
        else if (them.HasState)
        {
            // We take theirs. Forget what they sent before, so no part of their snapshot is dropped as a repeat.
            resetReceivedState();
            sendStateHello();   // our hello makes them send it (they decide by the same rule, so this ends)
        }
        else
        {
            // neither has any state, so no snapshot comes: both are blank, and the partner's first whole state merges with ours
            foreach (var t in wholeStateTypes)
                if (!lastReceivedState.ContainsKey(t)) assumePartnerHas(t);
        }
    }

    // ---- notes ----
    // The notes box syncs as one whole text, sent half a second after the typing stops. Each text carries the time of its last keystroke,
    // and both trackers keep the text with the later one (a tie goes to the lower console ID), so two players typing at once end up with the same notes.

    public sealed class NotesMessage
    {
        public string Text { get; set; }
        public long EditTicks { get; set; }   // UTC ticks of the last keystroke; 0 for notes not typed this run (loaded from Notes.txt)
    }

    public static bool isApplyingNotesFromSync = false;
    private static bool notesSubscribed = false;
    private static readonly Debouncer notesDebouncer = new Debouncer(500);
    private static long notesEditTicks = 0;      // the edit time of the text in the box (ours, or the partner's once applied)
    private static string lastAppliedNotes = null;

    public static string buildNotesJson() =>
        JsonConvert.SerializeObject(new NotesMessage { Text = OverworldItemGridUI.notesTextBox?.Text ?? "", EditTicks = notesEditTicks });

    /// UI thread.
    public static void sendNotesUpdate()
    {
        try
        {
            var json = buildNotesJson();
            if (shouldSend("Notes", json)) trySend("Notes", json);
        }
        catch (Exception ex) { TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send Notes: {ex.Message}"); }
    }

    /// Once, after the run has started (the notes box exists then). UI thread.
    public static void subscribeToNotesChanges()
    {
        var box = OverworldItemGridUI.notesTextBox;
        if (notesSubscribed || box == null) return;
        notesSubscribed = true;
        box.TextChanged += (_, _) =>
        {
            if (isApplyingNotesFromSync) return;
            if (lastAppliedNotes != null && box.Text == lastAppliedNotes) return;   // (the partner's text, if TextChanged is raised after the assignment)
            lastAppliedNotes = null;
            notesEditTicks = DateTime.UtcNow.Ticks;
            noteLocalEdit();
            notesDebouncer.Trigger(() => Dispatcher.UIThread.Post(sendNotesUpdate));
        };
    }

    /// A partner's notes. UI thread.
    public static void receiveNotes(NotesMessage m, string senderId, long timestamp)
    {
        var box = OverworldItemGridUI.notesTextBox;
        if (box == null || m == null || m.Text == null) return;
        bool theirsWins;
        if (m.EditTicks != notesEditTicks)
            theirsWins = m.EditTicks > notesEditTicks;
        else if (m.EditTicks == 0)
            theirsWins = true;   // (neither was typed this run: this is the partner's snapshot, which we take)
        else
            theirsWins = string.CompareOrdinal(senderId ?? "", TrackerModelOptions.CoopSyncOptions.MyConsoleId ?? "") < 0;
        if (!theirsWins)
        {
            // ours is newer: make sure the partner gets it (it may have been sent before theirs arrived, or not sent yet)
            if (box.Text != m.Text)
            {
                lastSentHashes.TryRemove("Notes", out _);
                notesDebouncer.Trigger(() => Dispatcher.UIThread.Post(sendNotesUpdate));
            }
            return;
        }
        applyRemote(senderId, timestamp, () =>
        {
            notesEditTicks = m.EditTicks;
            if (box.Text != m.Text)
            {
                var caret = box.CaretIndex;
                isApplyingNotesFromSync = true;
                try
                {
                    lastAppliedNotes = m.Text;
                    box.Text = m.Text;
                    box.CaretIndex = Math.Min(caret, m.Text.Length);
                }
                finally { isApplyingNotesFromSync = false; }
            }
            rebaseline("Notes");
        });
    }

    // ---- partner presence (the green dot) ----
    // Each tracker pings every 15 s while connected; the partner counts as there when it was heard from in the last 40 s.

    private static long partnerSeenTicks = 0;   // UTC ticks
    public static bool PartnerPresent => DateTime.UtcNow.Ticks - Interlocked.Read(ref partnerSeenTicks) < TimeSpan.FromSeconds(40.0).Ticks;
    /// Raised on the UI thread when PartnerPresent changes.
    public static event Action PartnerPresenceChanged;
    private static bool lastReportedPresence = false;

    private static void reportPresence()
    {
        var now = PartnerPresent;
        if (now == lastReportedPresence) return;
        lastReportedPresence = now;
        PartnerPresenceChanged?.Invoke();
    }

    /// A message from the partner. Returns true when the partner was not counted as there before. Any thread.
    /// Only a partner that pings counts: an older tracker never pings, so it stays blue instead of going green and back with each message.
    private static volatile bool partnerPings = false;
    public static bool notePartnerSeen(bool isPing)
    {
        if (isPing) partnerPings = true;
        if (!partnerPings) return false;
        var wasThere = PartnerPresent;
        Interlocked.Exchange(ref partnerSeenTicks, DateTime.UtcNow.Ticks);
        Dispatcher.UIThread.Post(reportPresence);   // (lastReportedPresence is only touched on the UI thread)
        return !wasThere;
    }

    public static void sendPing()
    {
        if (!TrackerModelOptions.CoopSyncOptions.GetEnableCoop() || !SyncManager.IsConnected) return;
        SyncManager.EnqueueSync("Ping", JsonConvert.SerializeObject(Guid.NewGuid().ToString()), TrackerModelOptions.CoopSyncOptions.MyConsoleId);
    }

    private static bool pingLoopStarted = false;
    /// Once, when the window is set up (the start screen pings too). UI thread.
    public static void startPingLoop()
    {
        if (pingLoopStarted) return;
        pingLoopStarted = true;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15.0) };
        timer.Tick += (_, _) => { sendPing(); reportPresence(); };
        timer.Start();
        SyncManager.ConnectionStateChanged += () => Dispatcher.UIThread.Post(() =>
        {
            if (SyncManager.IsConnected) { sendPing(); requestStartupOptions(); sendStartupOptions(); }   // (a host that connects after its run started still sends its options)
            else { Interlocked.Exchange(ref partnerSeenTicks, 0); partnerPings = false; reportPresence(); }
        });
    }

    // ---- startup options: the host's go to the client ----
    // The host is the tracker that started its run first. It sends its start screen choices when its run starts and when a partner on the
    // start screen asks. A partner still on the start screen takes them: its checkboxes are set and the host's overworld button is highlighted.
    // (A tracker whose run has already started keeps its own options: they cannot change without restarting.)

    public sealed class StartupOptions
    {
        public int Quest { get; set; }       // the start button: 0 1st Quest, 1 2nd Quest, 2 Mixed 1Q, 3 Mixed 2Q (others: no button to highlight)
        public string Keys { get; set; }     // the start screen checkboxes, as in TrackerModelOptions.SavedStartupSelection
        public string Nonce { get; set; }
    }

    /// Set when a new run starts (null for a loaded save, whose options come from the file).
    public static StartupOptions hostStartupOptions = null;
    /// Set by the start screen while it is up; cleared when the run starts. UI thread.
    public static Action<StartupOptions> startScreenOptionsHandler = null;

    public static void sendStartupOptions()
    {
        var o = hostStartupOptions;
        if (o == null || !startupBaselineDone || !TrackerModelOptions.CoopSyncOptions.GetEnableCoop() || !SyncManager.IsConnected) return;
        // the overworld and the extras below can be changed mid-run (Overworld + Extras): send them as they are now, not as the run started
        var quest = o.Quest;
        try { var q = TrackerModel.owInstance.Quest.AsInt(); if (q >= 0 && q <= 3) quest = q; } catch { }
        var keys = new List<string>((o.Keys ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
        void setKey(string key, bool on, bool random)
        {
            keys.RemoveAll(k => k == key || k == key + "?");
            if (random) keys.Add(key + "?");
            else if (on) keys.Add(key);
        }
        setKey("boots", TrackerModel.BootsReplaceBoomerangs, TrackerModel.BootsRandom);
        setKey("epb", OverworldData.ExtraPBBlocks, OverworldData.ExtraPBRandom);
        setKey("erb", OverworldData.ExtraRaftBlocks, OverworldData.ExtraRaftRandom);
        setKey("dw", OverworldData.DeadWoodsEnabled, OverworldData.DeadWoodsRandom);
        setKey("lh", OverworldData.LostHillsEnabled, OverworldData.LostHillsRandom);
        setKey("mirror", TrackerModel.MirrorOverworld, false);
        var msg = new StartupOptions { Quest = quest, Keys = string.Join(",", keys), Nonce = Guid.NewGuid().ToString() };
        SyncManager.EnqueueSync("StartupOptions", JsonConvert.SerializeObject(msg), TrackerModelOptions.CoopSyncOptions.MyConsoleId);
    }

    public static void requestStartupOptions()
    {
        if (startupBaselineDone || startScreenOptionsHandler == null || !TrackerModelOptions.CoopSyncOptions.GetEnableCoop() || !SyncManager.IsConnected) return;
        SyncManager.EnqueueSync("StartupOptionsRequest", JsonConvert.SerializeObject(Guid.NewGuid().ToString()), TrackerModelOptions.CoopSyncOptions.MyConsoleId);
    }

    /// The host's options. UI thread.
    public static void applyStartupOptions(StartupOptions o)
    {
        if (o == null || startupBaselineDone) return;
        startScreenOptionsHandler?.Invoke(o);
    }

    public static string buildItemsJson()
    {
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

            // the tier each progressive item box was collected at, in the save's own format "boxname,tier;..." (older trackers send none)
            model.ProgressiveBoxTiers = buildBoxTiers();

            return JsonConvert.SerializeObject(model);
        }
    }

    public static string buildBoxTiers()
    {
        try
        {
            return string.Join(";", SaveAndLoad.namedBoxes()
                                               .Where(nb => TrackerModel.progressiveBoxTier.ContainsKey(nb.Item2))
                                               .Select(nb => $"{nb.Item1},{TrackerModel.progressiveBoxTier[nb.Item2]}"));
        }
        catch (Exception) { return ""; }   // (before the dungeon tracker exists there are no named boxes)
    }

    /// A partner's box tiers (from an Items message). null: an older tracker, left alone. A box for which keepOurs is true keeps our tier.
    /// UI thread, inside applyRemote, after the boxes themselves (their change handlers note a tier of their own).
    public static void applyBoxTiers(string tiers, Func<object, bool> keepOurs)
    {
        if (tiers == null) return;
        var byName = new Dictionary<string, TrackerModel.Box>();
        foreach (var nb in SaveAndLoad.namedBoxes()) byName[nb.Item1] = nb.Item2;
        var incoming = new Dictionary<TrackerModel.Box, int>();
        foreach (var t in tiers.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var p = t.Split(',');
            if (p.Length == 2 && byName.TryGetValue(p[0], out var b) && int.TryParse(p[1], out var tier))
                incoming[b] = tier;
        }
        var changed = false;
        foreach (var b in byName.Values)
        {
            if (keepOurs(b)) continue;
            var had = TrackerModel.progressiveBoxTier.TryGetValue(b, out var old);
            if (incoming.TryGetValue(b, out var tier))
            {
                if (!had || old != tier) { TrackerModel.progressiveBoxTier[b] = tier; changed = true; }
            }
            else if (had) { TrackerModel.progressiveBoxTier.Remove(b); changed = true; }
        }
        if (changed) TrackerModel.progressiveTierChanged.Trigger(null);   // (draw the boxes with their tiers)
    }

    public static bool isApplyingItemsFromSync = false;

    public static void subscribeToItemsChanges(string myConsoleId)
    {
        // Shared timestamp that all item-related changes update
        var lastChanged = new TrackerModel.LastChangedTime();

        // Helper to mark the model as changed — suppressed during remote apply to prevent echo
        void markChanged(object box)
        {
            if (!isApplyingItemsFromSync && !TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
            {
                lastChanged.SetNow();
                noteLocalEdit();
                if (startupBaselineDone) boxEditTicks[box] = DateTime.UtcNow.Ticks;   // (UI thread: box events come from clicks)
            }
        }

        // Top-level boxes
        onEvent(TrackerModel.sword2Box.Changed, _ => markChanged(TrackerModel.sword2Box));
        onEvent(TrackerModel.ladderBox.Changed, _ => markChanged(TrackerModel.ladderBox));
        onEvent(TrackerModel.armosBox.Changed, _ => markChanged(TrackerModel.armosBox));

        // Dungeon boxes
        for (int i = 0; i <= 8; i++)
        {
            var dungeon = TrackerModel.GetDungeon(i);
            foreach (var box in dungeon.Boxes)
            {
                var b = box;
                onEvent(box.Changed, _ => markChanged(b));
            }
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



    public static readonly Debouncer overworldDebouncer = new Debouncer(200);

    public static void sendOverworldUpdateDebounced(string myConsoleId)
    {
        overworldDebouncer.Trigger(() =>
        {
            // (once per burst of changes, after the debounce. Snapshot, send and the record of what was sent all run on the UI thread,
            // as a partner's update and its rebaseline do, so a snapshot taken before that update is never sent or recorded after it)
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var jsonPayload = buildOverworldJson();
                    if (jsonPayload != lastSentOverworldJson)
                    {
                        TrackerModelOptions.DebugConfig.Log($"[Sync] Sending Overworld update: {jsonPayload}");
                        if (trySend("Overworld", jsonPayload))
                            lastSentOverworldJson = jsonPayload;
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

    // The cells whose Changed we listen to. reinitializeOverworld (a quest switch) replaces every cell, so the handlers are moved to the new ones.
    private static TrackerModel.Cell[,] tileHandlersOn = null;
    private static readonly FSharpHandler<Unit>[,] tileHandlers = new FSharpHandler<Unit>[16, 8];
    private static bool tileQuestHookAdded = false;

    public static void subscribeToOverworldTileChanges(string myConsoleId)
    {
        var cells = TrackerModel.overworldMapMarks;
        if (ReferenceEquals(cells, tileHandlersOn)) return;   // (these cells already have our handlers)
        if (tileHandlersOn != null)
            for (int x = 0; x <= 15; x++)
                for (int y = 0; y <= 7; y++)
                    if (tileHandlers[x, y] != null) tileHandlersOn[x, y].Changed -= tileHandlers[x, y];
        tileHandlersOn = cells;
        if (!tileQuestHookAdded)
        {
            tileQuestHookAdded = true;
            // (fired at the end of reinitializeOverworld, once the new cells exist)
            onEvent(TrackerModel.overworldQuestChanged.Publish, _ => subscribeToOverworldTileChanges(TrackerModelOptions.CoopSyncOptions.MyConsoleId));
        }
        for (int x0 = 0; x0 <= 15; x0++)
        {
            for (int y0 = 0; y0 <= 7; y0++)
            {
                var x = x0; var y = y0;   // (each closure keeps its own x, y, as in F#)
                FSharpHandler<Unit> handler = (_, _) =>   // (a [<CLIEvent>], so a C# event)
                {
                    if (isApplyingOverworldFromSync || TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
                        return;   // a partner's update being applied: do not send it back
                    noteLocalEdit();
                    if (startupBaselineDone) tileEditTicks[x, y] = DateTime.UtcNow.Ticks;
                    // (F#: Async.Start on the thread pool, which read the model off the UI thread, and two quick edits could be sent out of order.
                    // Posted to the UI thread instead: it runs after the current click handler is done (so a shop's second item, set after this
                    // event, is included, as with the pool), and posts run in order.)
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => sendTile(x, y), Avalonia.Threading.DispatcherPriority.Background);
                };
                tileHandlers[x, y] = handler;
                cells[x, y].Changed += handler;
            }
        }
    }

    /// Sends one overworld tile as it is now. UI thread.
    public static void sendTile(int x, int y)
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
                    CircleValue = circ,
                    Quest = currentOverworldQuest(),
                    Extras = tileExtrasOf(x, y)
                };

                var jsonPayload = JsonConvert.SerializeObject(update);
                if (shouldSend("OverworldTile", jsonPayload))
                {
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Sending OverworldTile ({x},{y}): {jsonPayload}");
                    trySend("OverworldTile", jsonPayload);
                }
            }
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send OverworldTile ({x},{y}): {ex.Message}");
        }
    }

    /// A partner's full Overworld or Items message skipped some of our newer edits: send our state again so they get them. UI thread.
    public static void resendOverworld(string myConsoleId)
    {
        lastSentOverworldJson = "";
        sendOverworldUpdateDebounced(myConsoleId);
    }

    public static void resendItems(string myConsoleId)
    {
        lastSentHashes.TryRemove("Items", out _);
        sendItemsUpdate(myConsoleId);
        boxTiersBaseline = buildBoxTiers();
    }




    public static Task sendDungeonTriforceUpdate(string myConsoleId)
    {
        try
        {
            var json = buildTriforceJson();
            if (shouldSend("DungeonTriforce", json))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending DungeonTriforce update: {json}");
                trySend("DungeonTriforce", json);
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
                if (!isApplyingTriforceFromSync && !TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
                {
                    noteLocalEdit();
                    CustomComboBoxes.StartImmediate(sendDungeonTriforceUpdate(myConsoleId));   // (trySend checks that co-op is on)
                }
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

    public static readonly Debouncer dungeonMapsDebouncer = new Debouncer(200);
    public static string lastSentDungeonMapsPayload = "";
    // Test seam: lets the self-test run the DungeonMaps send path without switching co-op on (which would open a network connection).
    public static bool testForceCoop = false;




    public static void sendDungeonMapsUpdate(string myConsoleId)
    {
        dungeonMapsDebouncer.Trigger(() =>
        {
            // (all on the UI thread, which owns the dungeon state and applies the partner's maps: a remote import cannot be half read,
            // and a snapshot taken before a partner's update is never sent or recorded as sent after it)
            Dispatcher.UIThread.Post(() =>
            {
                if (FSharpOption<TrackerModel.DungeonTrackerInstance>.get_IsNone(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption))
                {
                    TrackerModelOptions.DebugConfig.Log("[Sync] Skipping DungeonMaps update: TrackerModel not yet initialized.");
                    return;
                }
                if (!coopActive()) return;   // (co-op off: the 9 maps are not serialized at all)

                try
                {
                    var jsonPayload = buildDungeonMapsJson();

                    var dungeonMapsChanged = jsonPayload != lastSentDungeonMapsPayload;
                    var dungeonMapsSend = dungeonMapsChanged && shouldSendUpdate("DungeonMaps", jsonPayload);
                    if (dungeonMapsChanged && !dungeonMapsSend && DateTime.UtcNow < autoMutedUntil)
                    {
                        // (an update dropped by the anti-spam mute used to be lost until the next edit: try again once the mute ends, on the UI thread
                        // since the debouncer is not thread safe. The mute is set by shouldSendUpdate above, so this must come after it.)
                        var wait = autoMutedUntil - DateTime.UtcNow + TimeSpan.FromMilliseconds(100);
                        Task.Delay(wait).ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() => sendDungeonMapsUpdate(myConsoleId)));
                    }
                    if (dungeonMapsSend)
                    {
                        TrackerModelOptions.DebugConfig.Log($"[Sync] DungeonMaps update: {jsonPayload}");
                        if (trySend("DungeonMaps", jsonPayload))
                            lastSentDungeonMapsPayload = jsonPayload;
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
                    noteLocalEdit();   // (remote applies do not stamp dungeonRoomModelChanged, so this is a local change)
                    if (coopActive()) sendDungeonMapsUpdate(myConsoleId);   // (co-op off: nothing is serialized; the hello on connect brings the maps in step)
                }
            }
        }
        CustomComboBoxes.StartImmediate(loop());
    }

    // Create a mutable to debounce updates
    // (one per dungeon: with a shared one, relabelling two dungeons within 200ms sent only the second)
    public static readonly string[] lastHiddenDungeonColorLabelPayloads = Enumerable.Repeat("", 9).ToArray();
    public static readonly Debouncer[] hiddenDungeonColorLabelDebouncers = Enumerable.Range(0, 9).Select(_ => new Debouncer(200)).ToArray();

    public static void subscribeToHiddenDungeonColorLabelChanges(string myConsoleId)
    {
        // (co-op can be turned on after startup, so it is checked when sending, not when subscribing)
        if (TrackerModel.IsHiddenDungeonNumbers.Invoke(null))
        {
            for (int i0 = 0; i0 <= 8; i0++)
            {
                var i = i0;   // (each closure keeps its own i, as in F#)
                var dungeon = TrackerModel.GetDungeon(i);
                onEvent(dungeon.HiddenDungeonColorOrLabelChanged, t =>
                {
                    var (color, labelChar) = t;
                    if (TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
                        return;   // a partner's label being applied: do not send it back
                    noteLocalEdit();
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
                            hiddenDungeonColorLabelDebouncers[i].Trigger(() =>
                            {
                                Task.Run(() =>   // (F#: Async.Start)
                                {
                                    if (jsonPayload != lastHiddenDungeonColorLabelPayloads[i] && trySend("HiddenDungeonColorLabel", jsonPayload))
                                        lastHiddenDungeonColorLabelPayloads[i] = jsonPayload;
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
    // (F#: the body is an `async { }` started with Async.StartImmediate; nothing in it awaits, so it runs right here)
    // isResend: our value sent again after it won a conflict, not a new edit (its edit time stays)
    public static void sendDoorChangeUpdate(DungeonUI.DoorChangeInfo info, string myConsoleId, bool isResend = false)
    {
        var key = doorEditKey(info.Level, info.X, info.Y, info.IsHorizontal);
        try
        {
            var jsonPayload = JsonConvert.SerializeObject(info, new DoorStateAsUnionCaseConverter());
            if (!isResend)
            {
                noteLocalEdit();
                recordSend(key);   // (our edit time for this door, used by localEditWins and the DungeonMaps merge, even if co-op is off)
            }
            if (shouldSend("DoorChange", jsonPayload))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending DoorChange update: {jsonPayload}");

                var doorSyncReady = Dispatcher.UIThread.Invoke(() => isDoorSyncReady());

                if (doorSyncReady && trySend("DoorChange", jsonPayload))
                {
                    SyncManager.MarkLastSentDoorChange(jsonPayload);
                    // (a partner's change to this door identical to one we applied before is new again)
                    lastAppliedDoorPayloads.TryRemove(makeDoorKey(info.Level, info.X, info.Y, info.IsHorizontal), out _);
                }
                else if (!doorSyncReady)
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
            if (!TrackerModelOptions.CoopSyncOptions.IsBulkDoorInit && !isApplyingRoomFromSync && !TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
                sendDoorChangeUpdate(info, myConsoleId);
        };
    }

    // (F#: the body is an `async { }` started with Async.StartImmediate; nothing in it awaits, so it runs right here)
    // isResend: as for sendDoorChangeUpdate
    public static void sendRoomChangeUpdate(int level, int x, int y, Z1R_Tracker.Models.CDungeonRoomState room, string myConsoleId, bool isResend = false)
    {
        var key = roomEditKey(level, x, y);
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
                if (!isResend && !(DungeonPopups.suppressRoomSyncTemporarily))
                {
                    noteLocalEdit();
                    recordSend(key);   // (our edit time for this room, used by localEditWins and the DungeonMaps merge, even if co-op is off)
                }
                if (shouldSend("RoomChange", json) && !(DungeonPopups.suppressRoomSyncTemporarily) && trySend("RoomChange", json))
                {
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Sent RoomChange: {json}");
                    // (a partner's change to this room identical to one we applied before is new again)
                    lastAppliedRoomPayloads.TryRemove(makeRoomKey(level, x, y), out _);
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
                if (!isApplyingRoomFromSync && !(DungeonPopups.suppressRoomSyncTemporarily) && !TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
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
}
