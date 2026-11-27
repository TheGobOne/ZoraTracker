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

    public static ConcurrentDictionary<string, string> lastAppliedHashes = new ConcurrentDictionary<string, string>();
    public static ConcurrentDictionary<string, string> lastSentHashes = new ConcurrentDictionary<string, string>();
    public static bool isDoorSyncReady() =>
        TrackerModelOptions.CoopSyncOptions.GetEnableCoop() &&
        FSharpOption<TrackerModel.DungeonTrackerInstance>.get_IsSome(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption);

    public static readonly SyncOriginTracker dungeonMapsSyncOrigin = new SyncOriginTracker();
    private static readonly int hashRetentionLimit = 20;
    private static readonly ConcurrentDictionary<string, List<string>> recentReceivedHashes = new ConcurrentDictionary<string, List<string>>();


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
        checkMaxHeartsChange();   // (as for the extras: a max hearts click made less than a poll ago counts as ours)
        var saved = TrackerModelOptions.currentSyncSource;
        TrackerModelOptions.currentSyncSource = FSharpOption<string>.Some(senderId ?? "partner");
        try { apply(); }
        finally { TrackerModelOptions.currentSyncSource = saved; }
        noteRemoteState(timestamp);
    }

    /// Forgets every record of what the partner sent, so the snapshot that follows a hello is applied in full, even parts identical to older messages.
    /// (The versions stay: they say which edits each tracker has, and a snapshot is merged by them like any other message.)
    public static void resetReceivedState()
    {
        recentReceivedHashes.Clear();
        lastAppliedHashes.Clear();
    }

    // ---- versions: how the two trackers settle changes, including changes made at the same time ----
    // Every synced field or unit has a key (a PlayerProgress flag, a blocker, an item box, an overworld tile, a room, a door, ...) and a version:
    // a number and the writer (console ID plus this run's start time). A change made here sets the number to the highest one seen anywhere (the counter,
    // see versionClock), plus 1, and the writer to us. Each message carries the versions of what it holds, and our counter. The receiver takes a value
    // only when its version is higher than its own, comparing the number first and then the writer text, so both trackers decide the same way,
    // with no clocks involved. A stale echo (a partner's message built before our change reached them) has a lower version and changes nothing.
    // Changes made here are found by comparing the state with what it was when last looked at (see stamp), so a change has its version before it is sent.
    // Nothing of this is saved: after a reload the versions start again from 0, and the hello brings the trackers back in step. Edits made after the
    // reload but before the partner's counter is heard are renumbered above it then (see notePartnerClock), so they are not lost to older edits.
    // A value that cannot be applied (an item allowed once, still in another box) keeps ours, with a version above theirs (see endMerge).

    private static readonly Dictionary<string, (long v, string w)> versions = new Dictionary<string, (long v, string w)>();   // (UI thread only, as all of this section)
    private static readonly Dictionary<string, Dictionary<string, string>> versionBaselines = new Dictionary<string, Dictionary<string, string>>();   // per kind: key -> value when last looked at
    private static readonly HashSet<string> unsentEdits = new HashSet<string>();   // kinds with changes versioned here and not sent yet
    private static readonly HashSet<string> mustSend = new HashSet<string>();      // kinds to send even without new changes (the partner lacks a version of ours, or a hello snapshot)
    private static readonly HashSet<string> snapshotPending = new HashSet<string>();   // kinds whose next message is a hello snapshot
    private static readonly Dictionary<string, Dictionary<string, string>> lastUnversioned = new Dictionary<string, Dictionary<string, string>>();   // an older partner's last whole state of each kind
    private static string sessionNonce = "";

    // One running counter for this tracker: the highest version number made here or seen from the partner (in a message or a hello).
    // A new edit gets max(counter, the key's version) + 1, so an edit made after hearing from the partner is above every number they had.
    private static long versionClock = 0;
    // Edits made in this run before the partner's counter was first heard. After a reload our numbers start at 0 again while the partner's
    // do not, so these are given new numbers above the partner's counter once it is heard (see notePartnerClock).
    private static bool partnerClockKnown = false;
    private static readonly HashSet<string> preContactEdits = new HashSet<string>();
    private static readonly Dictionary<string, long> localEditTicks = new Dictionary<string, long>();   // key -> when we last versioned an edit of it (UTC ticks)
    private static readonly Dictionary<string, int> applyFailures = new Dictionary<string, int>();     // key -> how often the partner's value could not be applied

    /// The version number a new edit gets: above the counter and above the key's own version. (A pure rule, checked by the self-test.)
    /// (at long.MaxValue it stays there instead of wrapping to a negative number, which made every later edit of that key lose)
    public static long stampNumber(long clock, long keyVersion)
    {
        var m = Math.Max(clock, keyVersion);
        return m >= long.MaxValue ? long.MaxValue : m + 1;
    }

    /// The highest version number taken from a partner: 2^40, far above any real count of edits. A larger one (a damaged or hostile message)
    /// would raise our counter so far that our own edits of that key could never win again, so it is ignored.
    public const long MaxPartnerVersion = 1L << 40;
    public static bool versionInBounds(long v) => v >= 0 && v <= MaxPartnerVersion;
    /// A version number from a message: a whole number from 0 to MaxPartnerVersion, else false. (A number too big for a long is refused too.)
    public static bool readVersionNumber(Newtonsoft.Json.Linq.JToken t, out long v)
    {
        v = 0;
        if (!(t is Newtonsoft.Json.Linq.JValue jv) || jv.Type != Newtonsoft.Json.Linq.JTokenType.Integer) return false;
        if (jv.Value is long l) v = l;
        else if (jv.Value is int i) v = i;
        else return false;   // (a BigInteger: far above the bound)
        return versionInBounds(v);
    }

    private static void setVersion(string key, (long v, string w) x)
    {
        versions[key] = x;
        if (x.v > versionClock) versionClock = x.v;
    }

    /// The kind of a key ("PlayerProgress/PlayerHasBombs" -> "PlayerProgress").
    private static string kindOfKey(string key)
    {
        var i = key.IndexOf('/');
        return i < 0 ? key : key.Substring(0, i);
    }

    /// The partner's counter, from its hello or a message. Raises ours to it. The first one heard in this run gives our edits made before it
    /// new numbers above it, so an edit made right after a reload is not outranked by the partner's older, higher numbers. UI thread.
    public static void notePartnerClock(long partnerMax)
    {
        if (!versionInBounds(partnerMax))
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Ignored the partner's counter {partnerMax}: above {MaxPartnerVersion}");
            return;
        }
        if (partnerMax > versionClock) versionClock = partnerMax;
        if (partnerClockKnown) return;
        partnerClockKnown = true;
        foreach (var key in preContactEdits)
            if (versions.TryGetValue(key, out var v) && v.w == myWriter)   // (still our edit: the partner's value did not replace it)
            {
                setVersion(key, (stampNumber(versionClock, v.v), myWriter));
                unsentEdits.Add(kindOfKey(key));
            }
        if (preContactEdits.Count > 0) TrackerModelOptions.DebugConfig.Log($"[Sync] Renumbered {preContactEdits.Count} edits made before the partner was heard (above {partnerMax})");
        preContactEdits.Clear();
    }

    /// Our counter, for the hello.
    public static long maxVersion => versionClock;

    /// Test seam for the co-op test: the counter back to 0, as a real reload (a new process) has it. Call startupFinished after it.
    public static void testResetCounter() => versionClock = 0;

    /// Test seam for the self-test: sets (or with null removes) one key's version, without touching the counter.
    public static void testSetVersion(string key, (long v, string w)? x)
    {
        if (key == null) return;
        if (x is (long, string) xv) versions[key] = xv; else versions.Remove(key);
    }

    /// Our writer name: the console ID and this run's start time (so an edit made after a reload never has the same version as one made before it).
    public static string myWriter => (TrackerModelOptions.CoopSyncOptions.MyConsoleId ?? "") + "#" + sessionNonce;

    /// The kinds of message that carry a whole state with versions. (RoomChange, DoorChange, OverworldTile and HiddenDungeonColorLabel carry one unit of
    /// DungeonMaps, Overworld or Items, with that unit's version.)
    public static readonly string[] versionedKinds = { "PlayerProgress", "StartingItems", "Blockers", "DungeonTriforce", "Hints", "Items", "Overworld", "DungeonMaps" };

    /// The version rule: above 0 when (v1, w1) is newer than (v2, w2). The number decides; equal numbers go by the writer text (ordinal).
    public static int compareVersions(long v1, string w1, long v2, string w2) =>
        v1 != v2 ? v1.CompareTo(v2) : Math.Sign(string.CompareOrdinal(w1 ?? "", w2 ?? ""));

    /// Does a partner's value with this version replace ours? A newer version wins. A hello snapshot also wins a tie
    /// (both still at version 0, e.g. two different saves loaded: the hello winner's state is taken).
    public static bool incomingWins(long inV, string inW, long ourV, string ourW, bool snapshot)
    {
        var c = compareVersions(inV, inW, ourV, ourW);
        return c > 0 || (c == 0 && snapshot);
    }

    /// A message from an older tracker has no versions. As the old rule did: a value it changed since its last message of that kind is a new edit
    /// of theirs and is taken (when it differs from ours); a value it did not change keeps ours. Its first message is taken whole.
    /// Except that a value we changed here in the last 5 s keeps ours (the message may have been built before our change reached them, and then
    /// it holds our own older value: a stale echo). Our state is then sent again.
    public static bool unversionedTakes(string theirPrevious, string theirs, string ours, bool weEditedRecently = false) =>
        (theirPrevious == null || theirPrevious != theirs) && theirs != ours && !weEditedRecently;
    private static readonly long recentEditTicks = TimeSpan.FromSeconds(5.0).Ticks;
    private static bool editedRecently(string key) => localEditTicks.TryGetValue(key, out var t) && DateTime.UtcNow.Ticks - t < recentEditTicks;

    public static (long v, string w) versionOf(string key) => key != null && versions.TryGetValue(key, out var x) ? x : (0L, "");

    // ---- keys ----
    public static string boxKey(string name) => "Items/box/" + name;
    public static string boxKey(TrackerModel.Box b)
    {
        if (b == null) return null;
        try
        {
            foreach (var nb in SaveAndLoad.tierBoxes())   // (the item boxes and the M/C/Tri Shuffle spots)
                if (ReferenceEquals(nb.Item2, b)) return boxKey(nb.Item1);
        }
        catch (Exception) { }   // (before the dungeon tracker exists there are no named boxes)
        return null;
    }
    public static string labelKey(int dungeon) => $"Items/label/{dungeon}";
    public static string mapKey(int dungeon) => $"Items/map/{dungeon}";
    public static string itemsFlagKey(string name) => "Items/" + name;
    public static string tileKey(int x, int y) => $"Overworld/tile/{x},{y}";
    public static string overworldKey(string name) => "Overworld/" + name;
    public static readonly string[] shopArrayNames = { "ShopStash", "CheapKeyShops" };
    private static string shopArrayOfKey(string key)   // "Overworld/ShopStash/2" -> "ShopStash"; null for any other key
    {
        foreach (var n in shopArrayNames)
            if (key.StartsWith(overworldKey(n) + "/", StringComparison.Ordinal)) return n;
        return null;
    }
    public static string roomKey(int level, int x, int y) => $"DungeonMaps/room/{level},{x},{y}";
    public static string doorKey(int level, int x, int y, bool isH) => $"DungeonMaps/{(isH ? "hdoor" : "vdoor")}/{level},{x},{y}";
    public static string circleKey(int level, int x, int y) => $"DungeonMaps/circle/{level},{x},{y}";
    public static string overlayKey(int level) => $"DungeonMaps/overlay/{level}";
    public static string freshKey(int level) => $"DungeonMaps/fresh/{level}";   // (the big numeral and the first-click shortcut, see DungeonUI.isFirstTimeClickingAnyRoom)
    public static string tileValue(int cur, int ed, int circ, string extras) => $"{cur},{ed},{circ}|{extras ?? ""}";

    /// Our state of this kind, as we would send it (without versions). UI thread.
    private static string buildStateJson(string kind)
    {
        switch (kind)
        {
            case "PlayerProgress": return buildPlayerProgressJson();
            case "StartingItems": return buildStartingItemsJson();
            case "Blockers": return buildBlockersJson();
            case "DungeonTriforce": return buildTriforceJson();
            case "Hints": return buildHintsJson();
            case "Items": return buildItemsJson();
            case "Overworld": return buildOverworldJson();
            case "DungeonMaps":
                if (FSharpOption<TrackerModel.DungeonTrackerInstance>.get_IsNone(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption)) return null;
                return buildDungeonMapsJsonCached();
            default: return null;
        }
    }

    // ---- the state cache ----
    // Every partner message stamps all 8 kinds before the merge and looks at them again after it, and the half second poll sends 6 of them,
    // so each kind's state was serialized, parsed and split into keys many times a second (DungeonMaps alone is about 1500 keys).
    // Now the parse and split run only when a kind's JSON text differs from the last one seen: the same text always gives the same keys.
    // DungeonMaps is not even serialized when nothing on the 9 maps changed: a short fingerprint of the exported maps is compared first.
    // Both are exact (no change is ever missed): the text, or the fingerprint, holds everything the state is made from.
    private static readonly Dictionary<string, (string json, Dictionary<string, string> state)> stateCache = new Dictionary<string, (string, Dictionary<string, string>)>();
    private static string dungeonMapsFingerprintCache = null, dungeonMapsJsonCache = null;
    /// How often the caches saved work (for the self-test).
    public static int stateCacheHits = 0, dungeonMapsCacheHits = 0;

    /// Forgets the cached states (a new run: the item boxes may have other names). UI thread.
    public static void clearStateCache()
    {
        stateCache.Clear();
        dungeonMapsFingerprintCache = null;
        dungeonMapsJsonCache = null;
    }

    /// This kind's state as key -> value, from our JSON of it. The same text as last time gives the same (shared, never changed) dictionary. UI thread.
    private static Dictionary<string, string> stateOfJson(string kind, string json)
    {
        if (json == null) return null;
        if (stateCache.TryGetValue(kind, out var c) && c.json == json) { stateCacheHits++; return c.state; }
        var s = stateFromPayload(kind, Newtonsoft.Json.Linq.JToken.Parse(json));
        if (startupBaselineDone) stateCache[kind] = (json, s);   // (before the run starts the item boxes have no names yet)
        return s;
    }

    /// Our state of this kind as key -> value. null when it cannot be built yet. UI thread.
    /// (The dictionary may be shared with the cache: read it, never change it.)
    private static Dictionary<string, string> stateOf(string kind)
    {
        try
        {
            return stateOfJson(kind, buildStateJson(kind));
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Could not read the {kind} state: {ex.Message}");
            return null;
        }
    }

    /// Test seam for the self-test: our state of this kind as stateOf has it (null when it cannot be built).
    public static Dictionary<string, string> testStateOf(string kind) => stateOf(kind);

    /// Everything buildDungeonMapsJson writes, as one short text: each level's numeral flag, overlay, doors, circles and rooms (every field of
    /// DungeonRoomModel). The same fingerprint means the same JSON. (A field added to DungeonModel or DungeonRoomModel must be added here too.)
    public static string dungeonMapsFingerprint(DungeonSaveAndLoad.DungeonModel[] models)
    {
        var sb = new StringBuilder(6000);
        void ints(int[][] a)
        {
            if (a == null) { sb.Append('n'); return; }
            foreach (var row in a)
            {
                if (row == null) sb.Append('n');
                else foreach (var v in row) sb.Append(v).Append(',');
                sb.Append(';');
            }
        }
        for (int i = 0; i < models.Length; i++)
        {
            var m = models[i];
            sb.Append('L').Append(i).Append(DungeonUI.isFirstTimeClickingAnyRoom[i].Value ? 'F' : 'f').Append(m.VanillaMapOverlay).Append('|');
            ints(m.HorizontalDoors); sb.Append('|');
            ints(m.VerticalDoors); sb.Append('|');
            if (m.RoomIsCircled == null) sb.Append('n');
            else foreach (var row in m.RoomIsCircled)
                {
                    if (row == null) sb.Append('n');
                    else foreach (var v in row) sb.Append(v ? '1' : '0');
                    sb.Append(';');
                }
            sb.Append('|');
            if (m.RoomStates == null) sb.Append('n');
            else foreach (var row in m.RoomStates)
                {
                    if (row == null) sb.Append('n');
                    else foreach (var r in row)
                        {
                            if (r == null) { sb.Append("n\u0001"); continue; }
                            sb.Append(r.IsCompleted ? '1' : '0').Append(r.RoomType).Append('\u0002').Append(r.MonsterDetail).Append('\u0002')
                              .Append(r.FloorDropDetail).Append('\u0002').Append(r.FloorDropShouldAppearBright ? '1' : '0').Append('\u0001');
                        }
                    sb.Append(';');
                }
            sb.Append('#');
        }
        return sb.ToString();
    }

    /// buildDungeonMapsJson, but the 9 maps are serialized only when their fingerprint changed since the last call. UI thread.
    public static string buildDungeonMapsJsonCached()
    {
        var models = Enumerable.Range(0, 9).Select(i => DungeonUI.exportFunctionsLarge[i]()).ToArray();
        var fp = dungeonMapsFingerprint(models);
        if (fp == dungeonMapsFingerprintCache && dungeonMapsJsonCache != null) { dungeonMapsCacheHits++; return dungeonMapsJsonCache; }
        var json = dungeonMapsJsonFrom(models);
        dungeonMapsFingerprintCache = fp;
        dungeonMapsJsonCache = json;
        return json;
    }

    // The flat kinds are split into their JSON leaves (a Blockers message into its blockers). Key: kind/path, e.g. "PlayerProgress/PlayerHasBombs".
    private static int unitDepth(string kind) => kind == "Blockers" ? 2 : int.MaxValue;
    private static void flattenTokens(Newtonsoft.Json.Linq.JToken t, string path, int unitDepth, int depth, Dictionary<string, Newtonsoft.Json.Linq.JToken> r)
    {
        if (depth >= unitDepth || !(t is Newtonsoft.Json.Linq.JContainer))
            r[path] = t;
        else if (t is Newtonsoft.Json.Linq.JObject o)
        {
            foreach (var p in o.Properties())
                if (p.Name != "SyncV" && p.Name != "SyncSnap")
                    flattenTokens(p.Value, path + "/" + p.Name, unitDepth, depth + 1, r);
        }
        else if (t is Newtonsoft.Json.Linq.JArray a)
            for (int k = 0; k < a.Count; k++)
                flattenTokens(a[k], path + "/" + k, unitDepth, depth + 1, r);
    }

    /// A message of this kind (ours or the partner's, without its versions) as key -> value.
    public static Dictionary<string, string> stateFromPayload(string kind, Newtonsoft.Json.Linq.JToken t)
    {
        var r = new Dictionary<string, string>();
        switch (kind)
        {
            case "Items": itemsState(t, r); break;
            case "Overworld": overworldState(t, r); break;
            case "DungeonMaps": dungeonMapsState(t, r); break;
            default:
                var toks = new Dictionary<string, Newtonsoft.Json.Linq.JToken>();
                flattenTokens(t, kind, unitDepth(kind), 0, toks);
                foreach (var kv in toks) r[kv.Key] = kv.Value.ToString(Formatting.None);
                break;
        }
        return r;
    }

    private static int intOf(Newtonsoft.Json.Linq.JToken t, int d) => t != null && (t.Type == Newtonsoft.Json.Linq.JTokenType.Integer || t.Type == Newtonsoft.Json.Linq.JTokenType.Float) ? (int)t : d;

    // Items: each box is one unit (item, got, progressive tier), plus each dungeon's color and label, its map, and the two dungeon flags.
    private static void itemsState(Newtonsoft.Json.Linq.JToken t, Dictionary<string, string> r)
    {
        if (!(t is Newtonsoft.Json.Linq.JObject)) return;
        var tierText = t["ProgressiveBoxTiers"];
        var tiers = new Dictionary<string, string>();
        var tiersKnown = tierText != null && tierText.Type == Newtonsoft.Json.Linq.JTokenType.String;   // (older trackers send none)
        if (tiersKnown)
            foreach (var e in ((string)tierText).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var q = e.Split(',');
                if (q.Length == 2) tiers[q[0]] = q[1];
            }
        Dictionary<TrackerModel.Box, string> names = null;
        try { names = SaveAndLoad.namedBoxes().ToDictionary(nb => nb.Item2, nb => nb.Item1); } catch (Exception) { return; }
        void box(Newtonsoft.Json.Linq.JToken b, TrackerModel.Box model)
        {
            if (b == null || b.Type != Newtonsoft.Json.Linq.JTokenType.Object || model == null || !names.TryGetValue(model, out var name)) return;
            r[boxKey(name)] = $"{intOf(b["CellCurrent"], -1)},{intOf(b["PlayerHas"], 0)},{(tiersKnown ? (tiers.TryGetValue(name, out var tier) ? tier : "-") : "?")}";
        }
        r[itemsFlagKey("HiddenDungeonNumbers")] = (t["HiddenDungeonNumbers"] ?? "").ToString();
        r[itemsFlagKey("SecondQuestDungeons")] = (t["SecondQuestDungeons"] ?? "").ToString();
        // (the 4th Sword's place and Progressive Items: only in messages from trackers that send them, and not in Legacy Mode)
        if (t[FourthSwordField] is Newtonsoft.Json.Linq.JValue fs && fs.Type == Newtonsoft.Json.Linq.JTokenType.Integer) r[fourthSwordKey] = fs.ToString(Formatting.None);
        if (t[ProgressiveField] is Newtonsoft.Json.Linq.JValue pr && pr.Type == Newtonsoft.Json.Linq.JTokenType.Boolean) r[progressiveKey] = (bool)pr ? "1" : "0";
        if (t["Level9TriforceCount"] is Newtonsoft.Json.Linq.JValue l9 && l9.Type == Newtonsoft.Json.Linq.JTokenType.Integer) r[level9TriforcesKey] = l9.ToString(Formatting.None);
        box(t["WhiteSwordBox"], TrackerModel.sword2Box);
        box(t["LadderBox"], TrackerModel.ladderBox);
        box(t["ArmosBox"], TrackerModel.armosBox);
        box(t["MagicalSwordBox"], TrackerModel.sword3Box);
        box(t["LetterBox"], TrackerModel.letterBox);
        box(t["WoodSwordBox"], TrackerModel.sword1Box);
        box(t["Level2StairBox"], TrackerModel.level2StairBox);
        if (t["Dungeons"] is Newtonsoft.Json.Linq.JArray ds)
            for (int i = 0; i < ds.Count && i <= 8; i++)
            {
                if (!(ds[i] is Newtonsoft.Json.Linq.JObject d)) continue;
                var model = TrackerModel.GetDungeon(i);
                r[labelKey(i)] = $"{intOf(d["Color"], 0)},{(d["LabelChar"] ?? "").ToString()}";
                r[mapKey(i)] = (d["PlayerHasMap"] ?? "").ToString();
                if (d["Boxes"] is Newtonsoft.Json.Linq.JArray bs)
                    for (int j = 0; j < bs.Count && j < model.Boxes.Length; j++)
                        box(bs[j], model.Boxes[j]);
                box(d["ExtraBox"], model.ExtraFloorBox);
            }
        // the M/C/Tri Shuffle window: its Anywhere spots ("item,got,level") and its Between Levels slots ("collected,level")
        if (t[ShuffleSpotsField] is Newtonsoft.Json.Linq.JArray spots)
            for (int k = 0; k < spots.Count && k < 27; k++) r[boxKey("spot" + k)] = spots[k].ToString();
        if (t[ShuffleSlotsField] is Newtonsoft.Json.Linq.JArray slots)
            for (int k = 0; k < slots.Count && k < 27; k++) r[shuffleSlotKey(k)] = slots[k].ToString();
    }

    // ---- the M/C/Tri Shuffle window ----
    // Its state travels in the Items message in fields of its own, with versions like the boxes (3.2 and older trackers ignore them).
    // shortcut: a spot's progressive tier is not synced; add the spots to buildBoxTiers if partners show different tiers
    public const string ShuffleSpotsField = "ShuffleSpotStates";
    public const string ShuffleSlotsField = "ShuffleSlotStates";
    public static string shuffleSlotKey(int k) => itemsFlagKey($"slot/{k}");

    private static int[] shuffleNumbers(Newtonsoft.Json.Linq.JToken t, int count)   // "a,b,c" -> numbers; null when it is not that
    {
        var p = t.ToString().Split(',');
        if (p.Length != count) return null;
        var r = new int[count];
        for (int i = 0; i < count; i++) if (!int.TryParse(p[i], out r[i])) return null;
        return r;
    }
    private static int shuffleLevelOf(int v) => v >= 0 && v <= 8 ? v : -1;

    /// A partner's Anywhere spots, as box pairs for applyBoxes (an item moved between a box and a spot must not be lost). UI thread.
    public static void addShuffleSpotPairs(Newtonsoft.Json.Linq.JToken payload, List<(SaveAndLoad.Box src, TrackerModel.Box dest)> pairs)
    {
        if (!TrackerModel.ShuffleAnywhere || !(payload[ShuffleSpotsField] is Newtonsoft.Json.Linq.JArray spots)) return;
        for (int k = 0; k < spots.Count && k < 27; k++)
            if (shuffleNumbers(spots[k], 3) is int[] n)
                pairs.Add((new SaveAndLoad.Box { CellCurrent = n[0], PlayerHas = n[1] }, TrackerModel.shuffleSlotBoxes[k]));
    }

    /// After applyBoxes: the level each taken spot's item is for, and the partner's Between Levels slots. UI thread, inside applyRemote.
    public static void applyShuffleWindow(Merge m, Newtonsoft.Json.Linq.JToken payload)
    {
        if (TrackerModel.ShuffleAnywhere)
        {
            if (!(payload[ShuffleSpotsField] is Newtonsoft.Json.Linq.JArray spots)) return;
            for (int k = 0; k < spots.Count && k < 27; k++)
            {
                var b = TrackerModel.shuffleSlotBoxes[k];
                if (m.Takes(boxKey(b)) && shuffleNumbers(spots[k], 3) is int[] n && b.CellCurrent() == n[0] && b.PlayerHas().AsInt() == n[1])
                    b.AttemptToSetForLevel(n[0], b.PlayerHas(), shuffleLevelOf(n[2]));
            }
        }
        else if (payload[ShuffleSlotsField] is Newtonsoft.Json.Linq.JArray slots)
            for (int k = 0; k < slots.Count && k < 27; k++)
                if (m.Takes(shuffleSlotKey(k)) && shuffleNumbers(slots[k], 2) is int[] n
                    && (TrackerModel.shuffleSlotGot[k] != (n[0] == 1) || TrackerModel.shuffleSlotChosen[k] != shuffleLevelOf(n[1])))
                    TrackerModel.SetShuffleSlot(k / 3, k % 3, n[0] == 1, shuffleLevelOf(n[1]));
    }

    // Overworld: each tile is one unit (icon, extra data, circle and the extras), plus the start spot, the waypoint, mirroring and the shop globals.
    private static void overworldState(Newtonsoft.Json.Linq.JToken t, Dictionary<string, string> r)
    {
        if (!(t is Newtonsoft.Json.Linq.JObject)) return;
        r[overworldKey("Mirror")] = (t["MirrorOverworld"] ?? "").ToString();
        r[overworldKey("Start")] = $"{intOf(t["StartIconX"], -1)},{intOf(t["StartIconY"], -1)}";
        r[overworldKey("Waypoint")] = $"{intOf(t["CustomWaypointX"], -1)},{intOf(t["CustomWaypointY"], -1)}";
        // the shop arrays: one key for the whole array (all a 3.1 tracker knows) and one per shop, so edits of two shops at once both stay (see applyShopGlobals)
        foreach (var name in shopArrayNames)
            if (t[name] is Newtonsoft.Json.Linq.JArray a)
            {
                r[overworldKey(name)] = a.ToString(Formatting.None);
                for (int k = 0; k < a.Count && k <= 5; k++) r[overworldKey($"{name}/{k}")] = a[k].ToString(Formatting.None);
            }
        var map = t["Map"] as Newtonsoft.Json.Linq.JArray;
        if (map == null || map.Count != 384) return;
        var extras = t["TileExtras"] as Newtonsoft.Json.Linq.JArray;
        if (extras != null && extras.Count != 128) extras = null;
        for (int j = 0; j <= 7; j++)
            for (int i = 0; i <= 15; i++)
            {
                var idx = (j * 16 + i) * 3;
                r[tileKey(i, j)] = tileValue(intOf(map[idx], -1), intOf(map[idx + 1], 0), intOf(map[idx + 2], 0), extras == null ? null : (string)extras[j * 16 + i]);
            }
    }

    // DungeonMaps: each room, door and circle is one unit, plus each level's vanilla map overlay and whether its big numeral still shows.
    // (the arrays are [row][column] as sent, see cloneAndFixDungeonModel)
    private static void dungeonMapsState(Newtonsoft.Json.Linq.JToken t, Dictionary<string, string> r)
    {
        if (!(t is Newtonsoft.Json.Linq.JArray levels)) return;
        Newtonsoft.Json.Linq.JToken at(Newtonsoft.Json.Linq.JToken a, int row, int col) =>
            a is Newtonsoft.Json.Linq.JArray ra && row < ra.Count && ra[row] is Newtonsoft.Json.Linq.JArray rc && col < rc.Count ? rc[col] : null;
        for (int level = 0; level < levels.Count && level <= 8; level++)
        {
            if (!(levels[level] is Newtonsoft.Json.Linq.JObject m)) continue;
            var L = level + 1;
            for (int x = 0; x <= 7; x++)
                for (int y = 0; y <= 7; y++)
                {
                    var room = at(m["RoomStates"], y, x);
                    if (room != null) r[roomKey(L, x, y)] = room.ToString(Formatting.None);
                    var circle = at(m["RoomIsCircled"], y, x);
                    if (circle != null) r[circleKey(L, x, y)] = circle.ToString(Formatting.None);
                    var h = x <= 6 ? at(m["HorizontalDoors"], y, x) : null;
                    if (h != null) r[doorKey(L, x, y, true)] = h.ToString(Formatting.None);
                    var v = y <= 6 ? at(m["VerticalDoors"], y, x) : null;
                    if (v != null) r[doorKey(L, x, y, false)] = v.ToString(Formatting.None);
                }
            if (m["VanillaMapOverlay"] != null) r[overlayKey(L)] = m["VanillaMapOverlay"].ToString(Formatting.None);
            if (m["Fresh"] != null) r[freshKey(L)] = m["Fresh"].ToString(Formatting.None);   // (older trackers send none)
        }
    }

    /// Looks at our state of this kind and gives every key that changed since the last look a new version from us. UI thread.
    /// now: the state, when the caller has it already.
    private static void stamp(string kind, Dictionary<string, string> now = null)
    {
        if (!startupBaselineDone) return;
        now ??= stateOf(kind);
        if (now == null) return;
        if (versionBaselines.TryGetValue(kind, out var before) && !ReferenceEquals(before, now))   // (the same cached state: nothing changed)
            foreach (var kv in now)
                if (!before.TryGetValue(kv.Key, out var old) || old != kv.Value)
                {
                    setVersion(kv.Key, (stampNumber(versionClock, versionOf(kv.Key).v), myWriter));
                    localEditTicks[kv.Key] = DateTime.UtcNow.Ticks;
                    if (!partnerClockKnown) preContactEdits.Add(kv.Key);
                    unsentEdits.Add(kind);
                }
        versionBaselines[kind] = now;
    }

    /// Versions every change made here so far, of every kind (before a partner's message is merged, so none of ours is missed). UI thread.
    public static void stampAll()
    {
        if (!startupBaselineDone || !coopActive()) return;
        foreach (var k in versionedKinds) stamp(k);
    }

    /// Takes our current state of every kind as already versioned (after a partner's update was applied: those values are theirs). UI thread.
    private static void rebaselineVersions()
    {
        foreach (var k in versionedKinds)
        {
            var now = stateOf(k);
            if (now != null) versionBaselines[k] = now;
        }
    }

    /// The versions this message carries, for these keys (all keys of the kind when null), as {"w":[writers],"k":{key:[number, writer index]}}.
    private static Newtonsoft.Json.Linq.JObject versionsJson(string kind, IEnumerable<string> keys)
    {
        var prefix = kind + "/";
        var writers = new List<string>();
        var k = new Newtonsoft.Json.Linq.JObject();
        foreach (var key in keys ?? versions.Keys.Where(x => x.StartsWith(prefix, StringComparison.Ordinal)))
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal) || !versions.TryGetValue(key, out var v) || v.v <= 0) continue;
            var wi = writers.IndexOf(v.w);
            if (wi < 0) { wi = writers.Count; writers.Add(v.w); }
            k[key.Substring(prefix.Length)] = new Newtonsoft.Json.Linq.JArray(v.v, wi);
        }
        // "m": our counter, so the partner hears it with every message (see notePartnerClock)
        return new Newtonsoft.Json.Linq.JObject { ["w"] = new Newtonsoft.Json.Linq.JArray(writers), ["k"] = k, ["m"] = versionClock };
    }

    // The versions go in the message's root object or, for an array (Blockers, DungeonMaps), in its first object. Older trackers ignore the extra field.
    private static Newtonsoft.Json.Linq.JObject versionCarrier(Newtonsoft.Json.Linq.JToken t)
    {
        while (t is Newtonsoft.Json.Linq.JArray a && a.Count > 0) t = a[0];
        return t as Newtonsoft.Json.Linq.JObject;
    }

    /// Our JSON of this kind with the versions added (keys: just these, or all of the kind). UI thread.
    private static string withVersions(string kind, string json, IEnumerable<string> keys, bool snapshot = false)
    {
        var t = Newtonsoft.Json.Linq.JToken.Parse(json);
        var c = versionCarrier(t);
        if (c == null) return json;
        c["SyncV"] = versionsJson(kind, keys);
        if (snapshot) c["SyncSnap"] = true;
        return t.ToString(Formatting.None);
    }

    /// Reads the versions of a partner's message and removes them from it. present is false for a message from an older tracker.
    /// Their counter is noted (a message without one counts its highest version).
    private static (bool present, bool snapshot, Dictionary<string, (long v, string w)> v) takeVersions(string kind, Newtonsoft.Json.Linq.JToken t)
    {
        var r = new Dictionary<string, (long v, string w)>();
        var c = versionCarrier(t);
        if (c == null || !(c["SyncV"] is Newtonsoft.Json.Linq.JObject sv)) return (false, false, r);
        var res = takeVersionsFrom(kind, c, sv, r);
        long max = 0;
        if (sv["m"] != null && !readVersionNumber(sv["m"], out max))
        {
            max = 0;   // (a counter above the bound is ignored; the keys' own numbers still count)
            TrackerModelOptions.DebugConfig.Log($"[Sync] Ignored the partner's {kind} counter {sv["m"]}: not a number from 0 to {MaxPartnerVersion}");
        }
        foreach (var x in r.Values) if (x.v > max) max = x.v;
        notePartnerClock(max);
        return res;
    }

    private static (bool present, bool snapshot, Dictionary<string, (long v, string w)> v) takeVersionsFrom(string kind, Newtonsoft.Json.Linq.JObject c, Newtonsoft.Json.Linq.JObject sv, Dictionary<string, (long v, string w)> r)
    {
        var snap = c["SyncSnap"] != null && c["SyncSnap"].Type == Newtonsoft.Json.Linq.JTokenType.Boolean && (bool)c["SyncSnap"];
        c.Remove("SyncV");
        c.Remove("SyncSnap");
        var writers = (sv["w"] as Newtonsoft.Json.Linq.JArray)?.Select(x => (string)x).ToList() ?? new List<string>();
        var refused = 0;
        if (sv["k"] is Newtonsoft.Json.Linq.JObject ks)
            foreach (var p in ks.Properties())
                if (p.Value is Newtonsoft.Json.Linq.JArray a && a.Count == 2)
                {
                    // (a number that is not a whole number from 0 to MaxPartnerVersion is refused: the key counts as having no version from them)
                    if (!readVersionNumber(a[0], out var n)) { refused++; continue; }
                    var wi = intOf(a[1], -1);
                    r[kind + "/" + p.Name] = (n, wi >= 0 && wi < writers.Count ? writers[wi] : "");
                }
        if (refused > 0) TrackerModelOptions.DebugConfig.Log($"[Sync] Ignored {refused} {kind} version(s) from the partner: not a number from 0 to {MaxPartnerVersion}");
        return (true, snap, r);
    }

    /// What a partner's message changes here: the keys whose value is taken, with their versions.
    /// The versions are recorded by endMerge, after the apply, and only for the keys that really took the partner's value.
    public sealed class Merge
    {
        public string Kind;
        public bool Versioned, Snapshot;
        public readonly Dictionary<string, (long v, string w)> Taken = new Dictionary<string, (long v, string w)>();
        /// The partner's value of each key, as stateOf writes it (null when not known: a one-unit message other than OverworldTile, or DungeonTriforce,
        /// whose value here can rightly differ). endMerge compares it with ours after the apply when CheckAll is set.
        public Dictionary<string, string> Theirs;
        /// Compare every taken key with Theirs after the apply (Items and Overworld, where an item or icon allowed once can refuse to move; any kind
        /// whose apply threw).
        public bool CheckAll;
        /// Keys whose apply failed (the handler says so). They keep our value.
        public readonly HashSet<string> Failed = new HashSet<string>();
        /// Our keys cleared to make room for the partner's value (an item or icon allowed once moved to another box or tile). They count as edits of ours.
        public readonly HashSet<string> Displaced = new HashSet<string>();
        /// The partner's message lacks a version of ours that is higher than theirs (a lost or crossing message): ours goes to them again.
        public bool Resend;
        /// Overworld: the shop arrays this message versions per shop (a 3.1 tracker versions only the whole array).
        public readonly HashSet<string> PerIndex = new HashSet<string>();
        /// (a whole shop array also counts as taken when one of its shops is: Program.cs passes the array only then, and applyShopGlobals takes it shop by shop)
        public bool Takes(string key) => key != null && !Failed.Contains(key)
            && (Taken.ContainsKey(key) || (shopArrayNames.Any(n => key == overworldKey(n)) && Taken.Keys.Any(k => k.StartsWith(key + "/", StringComparison.Ordinal))));
        public void Fail(string key) { if (key != null && Taken.ContainsKey(key)) Failed.Add(key); }
        /// The apply threw part way: every taken key is checked against the partner's value (or, with none known, counts as failed).
        public void ApplyThrew(Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Applying the partner's {Kind} failed part way: {ex.Message}");
            CheckAll = true;
            if (Theirs == null) foreach (var k in Taken.Keys) Failed.Add(k);
        }
    }

    private static void decide(Merge m, string key, (long v, string w) theirs)
    {
        var ours = versionOf(key);
        if (incomingWins(theirs.v, theirs.w, ours.v, ours.w, m.Snapshot)) m.Taken[key] = theirs;
        else if (compareVersions(theirs.v, theirs.w, ours.v, ours.w) < 0) m.Resend = true;
    }

    /// A partner's whole state of this kind (its versions are removed from payload). Decides key by key what is taken.
    /// UI thread, inside applyRemote, before the apply; call endMerge after it (also when the apply throws).
    public static Merge beginMerge(string kind, Newtonsoft.Json.Linq.JToken payload, string senderId)
    {
        stampAll();
        var (present, snap, inV) = takeVersions(kind, payload);
        var m = new Merge { Kind = kind, Versioned = present, Snapshot = snap };
        var theirs = stateFromPayload(kind, payload);
        // (3.1 and older trackers always send the default 8 without a version: that one is neither taken nor answered. Any other count without
        // a version is real, e.g. from a loaded save. A snapshot's 8 is answered when ours has a version, such as a pasted count)
        if (kind == "Items" && theirs.TryGetValue(level9TriforcesKey, out var l9) && l9 == "8" && !(inV.TryGetValue(level9TriforcesKey, out var l9v) && l9v.v > 0)
            && !(snap && versionOf(level9TriforcesKey).v > 0)) theirs.Remove(level9TriforcesKey);
        if (kind == "Overworld") shopMerge = m;
        if (present)
        {
            if (kind == "Overworld")
                foreach (var key in inV.Keys)
                    if (shopArrayOfKey(key) is string n) m.PerIndex.Add(n);
            foreach (var key in theirs.Keys)
            {
                // (a 3.1 tracker's array goes by its whole-array version alone: its shops have no versions, and ours must not make us resend each time)
                if (shopArrayOfKey(key) is string n && !m.PerIndex.Contains(n)) continue;
                decide(m, key, inV.TryGetValue(key, out var v) ? v : (0L, ""));
            }
            if (kind != "DungeonTriforce") m.Theirs = theirs;
            m.CheckAll = kind == "Items" || kind == "Overworld";
        }
        else
        {
            var ours = stateOf(kind) ?? new Dictionary<string, string>();
            lastUnversioned.TryGetValue(kind, out var prev);
            foreach (var kv in theirs)
            {
                string p = null;
                if (prev != null && !prev.TryGetValue(kv.Key, out p)) p = null;
                var o = ours.TryGetValue(kv.Key, out var ov) ? ov : null;
                var recent = editedRecently(kv.Key);
                if (unversionedTakes(prev == null ? null : p, kv.Value, o, recent))
                    m.Taken[kv.Key] = (stampNumber(versionClock, versionOf(kv.Key).v), senderId ?? "partner");   // (their change counts as newer than anything we have)
                else if (recent && kv.Value != o && (prev == null || p != kv.Value))
                    m.Resend = true;   // (our recent change was kept: make sure they get it)
            }
            lastUnversioned[kind] = theirs;
        }
        return m;
    }

    /// As beginMerge, for a message that carries one unit (RoomChange, DoorChange, OverworldTile, HiddenDungeonColorLabel).
    /// From an older tracker such a message is always a new edit of theirs, so it is taken.
    /// theirValue: the unit's value as stateOf writes it, when the handler knows it (checked after the apply).
    public static Merge beginMergeUnit(string kind, string key, Newtonsoft.Json.Linq.JToken payload, string senderId, string theirValue = null)
    {
        stampAll();
        var (present, snap, inV) = takeVersions(kind, payload);
        var m = new Merge { Kind = kind, Versioned = present, Snapshot = snap };
        if (present)
        {
            decide(m, key, inV.TryGetValue(key, out var v) ? v : (0L, ""));
            if (theirValue != null) { m.Theirs = new Dictionary<string, string> { [key] = theirValue }; m.CheckAll = true; }
        }
        else m.Taken[key] = (stampNumber(versionClock, versionOf(key).v), senderId ?? "partner");
        return m;
    }

    /// After a partner's update was applied: records the partner's versions of the keys that took their value. A key that did not (its apply failed,
    /// or our value still differs from theirs) keeps our value, and that value gets a version above theirs, so the partner takes ours and both
    /// end with one value. (Keeping our lower version made the partner send theirs again, which failed here again, without end.)
    /// Then our state counts as known (it is theirs, not an edit of ours), and if their message lacked a version of ours, or a key kept ours,
    /// ours goes back to them (resendUnit for a one-unit message, else our whole state of that kind). Never sent just because ours was kept:
    /// they take ours when our own message, with its higher version, arrives. UI thread, inside applyRemote.
    public static void endMerge(Merge m, Action resendUnit = null)
    {
        if (m != null)
        {
            Dictionary<string, string> now = null;
            if (m.Versioned && m.CheckAll && m.Theirs != null) now = stateOf(m.Kind);
            foreach (var kv in m.Taken)
            {
                var key = kv.Key;
                var failed = m.Failed.Contains(key);
                // (a value the shop stash rule changed after the apply is not a failure: it goes out as an edit of ours, below)
                // (nor is a whole shop array that was merged shop by shop: it can rightly differ from theirs, and its shops are checked by their own keys)
                if (!failed && now != null && m.Theirs.TryGetValue(key, out var t) && now.TryGetValue(key, out var o) && o != t && !reconciledKeys.Contains(key)
                    && !m.PerIndex.Any(a => key == overworldKey(a))) failed = true;
                if (!failed) { setVersion(key, kv.Value); applyFailures.Remove(key); continue; }
                applyFailures.TryGetValue(key, out var n);
                applyFailures[key] = n + 1;
                if (n >= 3)
                {
                    // (two trackers that each cannot take the other's value would trade it forever: after a few tries the versions are let be)
                    setVersion(key, kv.Value);
                    TrackerModelOptions.DebugConfig.Log($"[Sync] {key}: the partner's value could not be applied again; giving up on it");
                    continue;
                }
                setVersion(key, (stampNumber(versionClock, kv.Value.v), myWriter));
                unsentEdits.Add(kindOfKey(key));
                m.Resend = true;
                TrackerModelOptions.DebugConfig.Log($"[Sync] {key}: the partner's value could not be applied; ours stays and goes back to them");
            }
            m.Displaced.UnionWith(reconciledKeys);   // (made from both sides' values, so the partner may not have them)
            foreach (var key in m.Displaced)
            {
                // (cleared here to make room for the partner's value: our edit, so the partner clears it too)
                setVersion(key, (stampNumber(versionClock, versionOf(key).v), myWriter));
                localEditTicks[key] = DateTime.UtcNow.Ticks;
                unsentEdits.Add(kindOfKey(key));
            }
            if (ReferenceEquals(shopMerge, m)) shopMerge = null;
        }
        reconciledKeys.Clear();
        rebaselineVersions();
        rebaseline("Hints");   // (the poll must not count a partner's hints as ours)
        boxTiersBaseline = buildBoxTiers();   // (nor a partner's box tiers)
        if (m == null || !m.Resend) return;
        TrackerModelOptions.DebugConfig.Log($"[Sync] The partner's {m.Kind} lacks newer versions of ours: sending ours again");
        if (resendUnit != null) Dispatcher.UIThread.Post(resendUnit);
        else resendKind(m.Kind);
    }

    /// Sends our whole state of this kind even without new changes. UI thread.
    public static void resendKind(string kind)
    {
        mustSend.Add(kind);
        lastSentHashes.TryRemove(kind, out _);   // (an identical earlier message was lost: send it again)
        sendKind(kind);
    }

    private static void sendKind(string kind)
    {
        var me = TrackerModelOptions.CoopSyncOptions.MyConsoleId;
        switch (kind)
        {
            case "PlayerProgress": sendPlayerProgressUpdate(me); break;
            case "StartingItems": sendStartingItemsAndExtrasUpdate(me); break;
            case "Blockers": sendBlockersUpdate(me); break;
            case "DungeonTriforce": sendDungeonTriforceUpdate(me); break;
            case "Hints": sendHintsUpdate(); break;
            case "Items": sendItemsUpdate(me); break;
            case "Overworld": sendOverworldUpdateDebounced(me); break;
            case "DungeonMaps": sendDungeonMapsUpdate(me); break;
        }
    }

    /// Sends the kinds that have changes versioned here and not sent (a change found when a partner's message was merged may have no send of its own
    /// on the way, such as a triforce counted from a box). Called by the hints poll. UI thread.
    private static void flushUnsent()
    {
        foreach (var k in versionedKinds)
            if (unsentEdits.Contains(k) || mustSend.Contains(k)) sendKind(k);
    }

    /// Stamps, then sends our whole state of this kind with its versions, if it has changes not sent yet (or must go anyway). UI thread.
    /// (Overworld and DungeonMaps come here after their debounce.)
    private static void sendWholeState(string kind)
    {
        if (!startupBaselineDone || !coopActive()) return;
        try
        {
            var json = buildStateJson(kind);
            if (json == null) return;
            stamp(kind, stateOfJson(kind, json));
            if (!unsentEdits.Contains(kind) && !mustSend.Contains(kind)) return;
            var payload = withVersions(kind, json, null, snapshotPending.Contains(kind));
            TrackerModelOptions.DebugConfig.Log($"[Sync] Sending {kind} update: {payload}");
            if (trySend(kind, payload)) { unsentEdits.Remove(kind); mustSend.Remove(kind); snapshotPending.Remove(kind); }
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Failed to send {kind} update: {ex.Message}");
        }
    }

    /// A partner's whole state of a flat kind (PlayerProgress, StartingItems, Blockers, DungeonTriforce, Hints): returns the JSON to apply,
    /// our state with the partner's value only where theirs won. UI thread, inside applyRemote; call endMerge after the apply.
    public static string mergeWholeState(string kind, string incomingJson, string senderId, out Merge m)
    {
        var incoming = Newtonsoft.Json.Linq.JToken.Parse(incomingJson);
        m = beginMerge(kind, incoming, senderId);
        var ours = Newtonsoft.Json.Linq.JToken.Parse(buildStateJson(kind));
        var inTok = new Dictionary<string, Newtonsoft.Json.Linq.JToken>();
        var ourTok = new Dictionary<string, Newtonsoft.Json.Linq.JToken>();
        flattenTokens(incoming, kind, unitDepth(kind), 0, inTok);
        flattenTokens(ours, kind, unitDepth(kind), 0, ourTok);
        foreach (var key in m.Taken.Keys)
            if (inTok.TryGetValue(key, out var t) && ourTok.TryGetValue(key, out var o) && o.Parent != null)
                o.Replace(t.DeepClone());
        return ours.ToString(Formatting.None);
    }

    /// Applies a partner's item boxes (pairs of their box and ours; only the taken ones whose item or got state changes). Two passes, as for the
    /// overworld tiles: first every box whose item changes is cleared, then each gets its new item. Setting in place lost an item moved between
    /// two boxes in one message (Bow from level 6 to level 3: level 3 refused it while level 6 still held it, then level 6 was cleared).
    /// A box that still cannot take its item gets its old one back and is marked failed (see endMerge). UI thread, inside applyRemote.
    public static void applyBoxes(Merge m, List<(SaveAndLoad.Box src, TrackerModel.Box dest)> pairs)
    {
        var todo = pairs.Where(p => p.src != null && p.dest != null && m.Takes(boxKey(p.dest))
                                    && (p.src.CellCurrent != p.dest.CellCurrent() || p.src.PlayerHas != p.dest.PlayerHas().AsInt())).ToList();
        var old = todo.Select(p => (cell: p.dest.CellCurrent(), has: p.dest.PlayerHas(), level: p.dest.ShuffleLevel)).ToList();
        for (int k = 0; k < todo.Count; k++)
            if (todo[k].src.CellCurrent != old[k].cell && todo[k].dest.LockedItem == -1)
                todo[k].dest.AttemptToSet(-1, old[k].has);
        for (int k = 0; k < todo.Count; k++)
            if (!applyBoxFromSync(m, todo[k].src, todo[k].dest))
            {
                // (ours stays as it was, if it still can; a spot keeps the level its item is for)
                if (old[k].level >= 0) todo[k].dest.AttemptToSetForLevel(old[k].cell, old[k].has, old[k].level);
                else todo[k].dest.AttemptToSet(old[k].cell, old[k].has);
            }
    }

    /// Puts a partner's item in one of our boxes. When the item is allowed once and another box of ours holds it, the newer version keeps it:
    /// our box is cleared if theirs is newer, else theirs is not taken (both trackers decide the same way). Returns false, and marks the key failed,
    /// when the box did not get it. UI thread, inside applyRemote.
    public static bool applyBoxFromSync(Merge m, SaveAndLoad.Box src, TrackerModel.Box dest)
    {
        var key = boxKey(dest);
        TrackerModel.PlayerHas ph;
        try { ph = TrackerModel.PlayerHas.FromInt(src.PlayerHas); } catch (Exception) { m.Fail(key); return false; }
        if (dest.AttemptToSet(src.CellCurrent, ph)) return true;
        if (src.CellCurrent >= 0 && dest.LockedItem == -1 && key != null && m.Taken.TryGetValue(key, out var theirs))
        {
            foreach (var nb in SaveAndLoad.tierBoxes())   // (a M/C/Tri Shuffle spot can hold the item too)
            {
                var h = nb.Item2;
                if (ReferenceEquals(h, dest) || !ReferenceEquals(h.Domain, dest.Domain) || h.CellCurrent() != src.CellCurrent || h.LockedItem != -1) continue;
                var hk = boxKey(nb.Item1);
                if (m.Takes(hk)) continue;   // (a box the message sets too: not ours to give up)
                var ours = versionOf(hk);
                if (!incomingWins(theirs.v, theirs.w, ours.v, ours.w, m.Snapshot)) continue;   // (our box's version is newer: it keeps the item)
                if (h.AttemptToSet(-1, TrackerModel.PlayerHas.NO))
                {
                    m.Displaced.Add(hk);
                    if (dest.AttemptToSet(src.CellCurrent, ph)) return true;
                }
            }
        }
        m.Fail(key);
        TrackerModelOptions.DebugConfig.Log($"[Sync] Could not put item {src.CellCurrent} in box {key}: ours stays");
        return false;
    }

    /// As applyBoxFromSync, for an overworld tile's icon (a dungeon number, say, allowed on one tile). UI thread, inside applyRemote.
    public static bool applyTileFromSync(Merge m, int x, int y, int value)
    {
        var cells = TrackerModel.overworldMapMarks;
        var key = tileKey(x, y);
        if (cells[x, y].AttemptToSet(value)) return true;
        if (value >= 0 && m.Taken.TryGetValue(key, out var theirs))
        {
            for (int j = 0; j <= 7; j++)
                for (int i = 0; i <= 15; i++)
                {
                    if ((i == x && j == y) || cells[i, j].Current() != value) continue;
                    var hk = tileKey(i, j);
                    if (m.Takes(hk)) continue;
                    var ours = versionOf(hk);
                    if (!incomingWins(theirs.v, theirs.w, ours.v, ours.w, m.Snapshot)) continue;
                    if (cells[i, j].AttemptToSet(-1))
                    {
                        m.Displaced.Add(hk);
                        if (cells[x, y].AttemptToSet(value)) return true;
                    }
                }
        }
        m.Fail(key);
        TrackerModelOptions.DebugConfig.Log($"[Sync] Could not set tile ({x},{y}) to {value}: ours stays");
        return false;
    }

    /// An OverworldTile message's tile as stateOf writes it (the extra data counts only for icons that have it, as the full Overworld message sends).
    public static string unitTileValue(int cur, int ed, int circ, string extras) =>
        extras == null ? null : tileValue(cur, cur >= 0 && cur <= TrackerModel.MapSquareChoiceDomainHelper.DARK_X ? ed : 0, circ, extras);


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

    /// Sends our room as it is now, with its version (the partner's message lacked it). UI thread.
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

    /// As resendRoom, for an overworld tile. UI thread.
    public static void resendTile(int x, int y)
    {
        lastSentHashes.TryRemove("OverworldTile", out _);
        sendTile(x, y);
    }

    /// A partner's room change was applied: our last sent RoomChange no longer describes our state, so the same change must be sendable again.
    public static void remoteRoomApplied() => lastSentHashes.TryRemove("RoomChange", out _);

    /// As remoteRoomApplied, for doors.
    public static void remoteDoorApplied()
    {
        lastSentHashes.TryRemove("DoorChange", out _);
        SyncManager.MarkLastSentDoorChange(null);
    }

    /// Is a got triforce in one of this dungeon's item slots (PlayerHasTriforce counts it)? Then the dungeon's own triforce flag cannot be read
    /// from PlayerHasTriforce, and a partner's "no triforce" must not toggle it (that set the flag, and the triforce stayed on after the box was cleared).
    public static bool triforceInBox(int i)
    {
        var d = TrackerModel.GetDungeon(i);
        bool got(TrackerModel.Box b) => b.CellCurrent() == TrackerModel.ITEMS.TRIFORCE && b.PlayerHas() == TrackerModel.PlayerHas.YES;
        return d.Boxes.Any(got) || (i == 1 && TrackerModel.level2StairBoxEnabled && got(TrackerModel.level2StairBox));
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
                // (the pause is the whole penalty: the burst that tripped it no longer counts. Kept in the window, it tripped the mute again
                // on the first send after the pause, so an update dropped by the mute was dropped a second time and went out a second late)
                syncBurstTimestamps.Clear();
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


    // (the senders below are called on the UI thread: from change handlers, the polling loops and the merge)
    public static Task sendPlayerProgressUpdate(string myConsoleId)
    {
        sendWholeState("PlayerProgress");
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
        sendWholeState("StartingItems");
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
        // (these two had no listener: their changes went out only with the next poll, and never counted as an edit of ours in the hello)
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasMeat.Changed, _ => sendUpdate());
        onEvent(TrackerModel.startingItemsAndExtras.PlayerHasLetter.Changed, _ => sendUpdate());
        // (MaxHeartsDifferential is a plain field with no event: checkMaxHeartsChange finds its changes)
    }

    // the max hearts differential (the Max Hearts clicks) as last seen: a change from it is a local edit
    private static int maxHeartsBaseline = 0;

    /// Finds a change of the max hearts differential made here and sends it. Called by the half second poll and before a partner's update
    /// is applied (so a change made just before is known as ours). UI thread.
    public static void checkMaxHeartsChange()
    {
        var now = TrackerModel.startingItemsAndExtras.MaxHeartsDifferential;
        if (now == maxHeartsBaseline || !startupBaselineDone) return;
        maxHeartsBaseline = now;
        if (TrackerModelOptions.isCurrentlyApplyingRemoteUpdate()) return;   // (a partner's value: the StartingItems handler sets the baseline too)
        noteLocalEdit();
        sendStartingItemsAndExtrasUpdate(TrackerModelOptions.CoopSyncOptions.MyConsoleId);   // (trySend checks that co-op is on)
    }

    public static Task sendBlockersUpdate(string myConsoleId)
    {
        sendWholeState("Blockers");
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
    public static void sendHintsUpdate() => sendWholeState("Hints");

    // The small kinds polled every half second, for changes that fire no event of their own (the hints, the meat and letter, the HDN starting triforces, ...)
    private static readonly string[] polledKinds = { "PlayerProgress", "StartingItems", "Blockers", "DungeonTriforce", "Hints", "Items" };

    private static bool hintsLoopStarted = false;
    /// Once, after the run has started. UI thread. (also the poll for the other small kinds, and the send of changes found by a merge)
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
                    checkMaxHeartsChange();
                    if (coopActive())
                    {
                        foreach (var k in polledKinds) sendKind(k);   // (each sends only when it has an unsent change)
                        flushUnsent();
                    }
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
        sendWholeState("Items");
        sendWholeState("DungeonTriforce");   // (a triforce marked as got in a box counts as the dungeon's triforce)
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
        model.Triforces = Enumerable.Range(0, 9).Select(i => TrackerModel.GetDungeon(i).PlayerHasOwnTriforce()).ToArray();
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
    /// reconcile false: part of a full Overworld message, whose stash is not here yet (applyShopGlobals applies the stash rule after all of it).
    public static bool applyTileExtras(int i, int j, string extras, bool reconcile = true)
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
        if (reconcile) reconcileShops();   // (the tile can come before or after the stash it takes)
        return true;
    }

    // The Overworld merge being applied (from beginMerge to endMerge), so applyShopGlobals can take the shop arrays shop by shop.
    private static Merge shopMerge;
    // Keys the shop stash rule changed during this apply (see reconcileShops and endMerge).
    private static readonly HashSet<string> reconciledKeys = new HashSet<string>();

    /// Applies the shop stash rule (WPFUI.reconcileShopStash) after a partner's tile or stash was put here. The result can hold a stash item of ours
    /// the partner never received, so endMerge versions it as an edit of ours, and the poll (its baselines are left as they were) sends it. A partner
    /// that already has the same value takes it without change, so this ends. UI thread, inside applyRemote.
    private static void reconcileShops()
    {
        var before = (int[])TrackerModel.shopStash.Clone();
        var tiles = WPFUI.reconcileShopStash();
        if (tiles.Count == 0 && before.SequenceEqual(TrackerModel.shopStash)) return;
        foreach (var (x, y) in tiles)
        {
            reconciledKeys.Add(tileKey(x, y));
            WPFUI.redrawOwTileHook(x, y);
        }
        for (int k = 0; k <= 5; k++)
            if (before[k] != TrackerModel.shopStash[k]) { reconciledKeys.Add(overworldKey($"ShopStash/{k}")); reconciledKeys.Add(overworldKey("ShopStash")); }
        WPFUI.decorStateChanged();
    }

    /// The partner's shop stash and cheap key flags, from a full Overworld message (null arrays: older tracker, left alone). Returns true when they changed. UI thread.
    /// Each shop goes by its own version, so two trackers that edit two different shops at once both keep both edits. (A 3.1 tracker sends one version for
    /// the whole array: then the array is taken whole, as 3.1 does.)
    public static bool applyShopGlobals(int[] stash, int[] cheapKeys)
    {
        var m = shopMerge;
        bool takes(string name, int k)
        {
            if (m == null || m.Takes(overworldKey($"{name}/{k}"))) return true;
            var whole = overworldKey(name);
            return m.Versioned && !m.PerIndex.Contains(name) && m.Taken.ContainsKey(whole) && !m.Failed.Contains(whole);
        }
        var changed = false;
        if (stash != null && stash.Length == 6)
            for (int k = 0; k <= 5; k++)
                if (TrackerModel.shopStash[k] != stash[k] && takes("ShopStash", k)) { TrackerModel.shopStash[k] = stash[k]; changed = true; }
        if (cheapKeys != null && cheapKeys.Length == 6)
            for (int k = 0; k <= 5; k++)
                if (TrackerModel.cheapKeyShops[k] != (cheapKeys[k] == 1) && takes("CheapKeyShops", k)) { TrackerModel.cheapKeyShops[k] = cheapKeys[k] == 1; changed = true; }
        shopStashBaseline = (int[])TrackerModel.shopStash.Clone();
        cheapKeyBaseline = (bool[])TrackerModel.cheapKeyShops.Clone();
        reconcileShops();   // (the stash can come before or after the tile that takes it)
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

    /// Finds tile extras (and shop stash) changes made here since the last look and sends them. UI thread.
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
                            // (a local change: remote applies update the baseline themselves; sendTile gives it its version)
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
    public static string buildDungeonMapsJson() => dungeonMapsJsonFrom(Enumerable.Range(0, 9).Select(i => DungeonUI.exportFunctionsLarge[i]()).ToArray());

    /// The DungeonMaps JSON of these exported maps (one per level, as exportFunctionsLarge gives them).
    private static string dungeonMapsJsonFrom(DungeonSaveAndLoad.DungeonModel[] exported)
    {
        var dungeonModels = exported.Select(cloneAndFixDungeonModel).ToArray();
        var arr = Newtonsoft.Json.Linq.JArray.FromObject(dungeonModels);
        // whether each level's big numeral (and the first-click shortcut) still shows, so both trackers show the same (older trackers ignore it)
        for (int i = 0; i < arr.Count; i++)
            if (arr[i] is Newtonsoft.Json.Linq.JObject o) o["Fresh"] = DungeonUI.isFirstTimeClickingAnyRoom[i].Value;
        return arr.ToString(Formatting.None);
    }

    /// The baselines of the polls that are not versions: the hints (a change counts as ours in the hello's state time), the box tiers (their poll
    /// sends Items), and the last notes sent. After a partner's update they must not count it as ours. UI thread.
    public static void rebaseline(string messageType)
    {
        try
        {
            switch (messageType)
            {
                case "Items": case "Overworld": boxTiersBaseline = buildBoxTiers(); break;
                case "Notes": lastSentHashes["Notes"] = computeHash(buildNotesJson()); break;
                case "Hints": hintsBaseline = buildHintsJson(); break;
                case "StartingItems": maxHeartsBaseline = TrackerModel.startingItemsAndExtras.MaxHeartsDifferential; break;
            }
        }
        catch (Exception ex)
        {
            TrackerModelOptions.DebugConfig.Log($"[Sync] Could not record the {messageType} state: {ex.Message}");
        }
    }

    /// Keys the start screen's flag paste set (Expanded Dungeon Locations circles, the Level 9 count). They are part of the startup baseline, so
    /// they would never be sent. They get the lowest version a value can have (it beats an untouched value and loses to any edit):
    /// the count at the start of the run, so it holds against a partner's snapshot; the tiles only when a hello says the partner has no state
    /// (a partner with state keeps its own tiles, as the hello rules say). Pasting never counts as having state. UI thread.
    public static readonly HashSet<string> startScreenEdits = new HashSet<string>();
    private static readonly HashSet<string> pendingStartScreenEdits = new HashSet<string>();
    private static void versionStartScreenKey(string key) { if (versionOf(key).v == 0) setVersion(key, (1, "")); }   // (the empty writer loses a tie to any real edit)
    private static void sendStartScreenEdits()
    {
        var kinds = pendingStartScreenEdits.Select(kindOfKey).Distinct().ToList();
        foreach (var k in pendingStartScreenEdits) versionStartScreenKey(k);
        pendingStartScreenEdits.Clear();
        foreach (var kind in kinds) resendKind(kind);   // (not a snapshot: only newer versions are taken, so nothing of the partner's is wiped)
    }

    /// Called once when a run has started (new or loaded), on the UI thread. Records the current state as the baseline,
    /// so only changes from now on are sent, then says hello to the partner if connected.
    /// loadedTicks: for a loaded save, the save file's last write time (UTC ticks), used as its state time in the hello.
    public static void startupFinished(bool loadedSave, long loadedTicks = 0)
    {
        foreach (var t in new[] { "Items", "Notes", "Hints", "StartingItems" })
            rebaseline(t);
        clearStateCache();
        // the versions start again: every field is at version 0 until it is changed (here or by the partner)
        versions.Clear();
        versionBaselines.Clear();
        unsentEdits.Clear();
        mustSend.Clear();
        snapshotPending.Clear();
        lastUnversioned.Clear();
        localEditTicks.Clear();
        applyFailures.Clear();
        preContactEdits.Clear();
        partnerClockKnown = false;   // (the counter itself is kept: a run started again in this process numbers on from it)
        sessionNonce = DateTime.UtcNow.Ticks.ToString("x");
        rebaselineVersions();
        pendingStartScreenEdits.Clear();
        if (!loadedSave) pendingStartScreenEdits.UnionWith(startScreenEdits);
        startScreenEdits.Clear();
        if (pendingStartScreenEdits.Contains(level9TriforcesKey)) versionStartScreenKey(level9TriforcesKey);
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

    /// Sends everything this tracker has, with its versions, marked as a hello snapshot. Used when the hello handshake picks this tracker's state.
    /// The partner merges it by the versions like any other message, so a change of theirs made before or while it travels is kept;
    /// only where both are still at the same version does the snapshot win (see incomingWins). UI thread.
    public static void sendFullSnapshot(string myConsoleId)
    {
        if (!startupBaselineDone) return;
        lastSentDungeonMapsPayload = "";
        lastSentHashes.Clear();
        foreach (var k in versionedKinds) { mustSend.Add(k); snapshotPending.Add(k); }
        sendPlayerProgressUpdate(myConsoleId);
        sendStartingItemsAndExtrasUpdate(myConsoleId);
        sendItemsUpdate(myConsoleId);
        sendBlockersUpdate(myConsoleId);
        sendDungeonTriforceUpdate(myConsoleId);
        sendOverworldUpdateDebounced(myConsoleId);
        sendDungeonMapsUpdate(myConsoleId);
        sendNotesUpdate();
        sendHintsUpdate();
    }

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
        public long? MaxVersion { get; set; }   // the sender's version counter (null from trackers without one), see notePartnerClock
        public bool Reply { get; set; }   // an answer to a blank partner's joining hello: never answered in turn
    }

    // We count as joining for a short time after our own joining hello, long enough for the handshake to finish.
    private static DateTime joiningUntil = DateTime.MinValue;
    private static bool meJoining() => DateTime.UtcNow < joiningUntil;

    /// joining: true when this tracker has just started a run or (re)connected. UI thread.
    public static void sendStateHello(bool joining = false, bool reply = false)
    {
        if (!startupBaselineDone || !TrackerModelOptions.CoopSyncOptions.GetEnableCoop()) return;
        if (joining)
        {
            joiningUntil = DateTime.UtcNow.AddSeconds(15.0);
            resetReceivedState();   // (we will take the partner's snapshot if they win: apply all of it)
        }
        StateHello hello;
        lock (stateLock) hello = new StateHello { HasState = hasState, StateTicks = stateTicks, Joining = meJoining(), Nonce = Guid.NewGuid().ToString(), MaxVersion = versionClock, Reply = reply };
        SyncManager.EnqueueSync("StateHello", JsonConvert.SerializeObject(hello), TrackerModelOptions.CoopSyncOptions.MyConsoleId);
    }

    /// A partner's hello. UI thread.
    public static void handleStateHello(StateHello them, string senderId)
    {
        if (!startupBaselineDone) return;   // our run has not started: we say hello ourselves once it has
        // (before anything is sent: our edits made since a reload get numbers above the partner's, and so does any edit from now on)
        if (them.MaxVersion is long partnerMax) notePartnerClock(partnerMax);
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
        // (neither has any state: no snapshot comes, both are blank. A joining partner is answered once, so one that started after us hears that
        // we are blank too and can send its pasted start screen values)
        else if (them.Joining && !them.Reply && !hasState) sendStateHello(reply: true);
        if (pendingStartScreenEdits.Count > 0)
        {
            if (!them.HasState) sendStartScreenEdits();
            else pendingStartScreenEdits.Clear();
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
            // (these four were never synced; older trackers ignore them)
            model.MagicalSwordBox = toSerializableBox(TrackerModel.sword3Box);
            model.LetterBox = toSerializableBox(TrackerModel.letterBox);
            model.WoodSwordBox = toSerializableBox(TrackerModel.sword1Box);
            model.Level2StairBox = toSerializableBox(TrackerModel.level2StairBox);   // the level 2 staircase slot (4th Sword)
            model.Level9TriforceCount = TrackerModel.level9TriforceCount;   // (3.1 ignores it)

            model.Dungeons =
                Enumerable.Range(0, 9).Select(i =>
                {
                    var d = new SaveAndLoad.Dungeon();
                    var trackerDungeon = TrackerModel.GetDungeon(i);

                    d.Triforce = false; // EXCLUDE this value — syncing Triforce separately
                    d.Color = trackerDungeon.Color;
                    d.LabelChar = trackerDungeon.LabelChar.ToString();
                    d.PlayerHasMap = trackerDungeon.PlayerHasOwnMap;
                    d.Boxes =
                        trackerDungeon.Boxes.Select(box => toSerializableBox(box)).ToArray();
                    d.ExtraBox = toSerializableBox(trackerDungeon.ExtraFloorBox);   // the dungeon's extra floor item slot (4th Sword)
                    return d;
                }).ToArray();

            // the tier each progressive item box was collected at, in the save's own format "boxname,tier;..." (older trackers send none)
            model.ProgressiveBoxTiers = buildBoxTiers();

            var json = JsonConvert.SerializeObject(model);
            if (TrackerModel.LegacyMode) return json;   // (the 4th Sword option is off and locked there: nothing of it is sent)
            // the 4th Sword's place and Progressive Items, in fields of their own (older trackers ignore them). Not in Level2StairBoxEnabled and
            // Level9FloorBoxEnabled: older trackers send those as false always, and that would read as "no 4th sword".
            var jo = Newtonsoft.Json.Linq.JObject.Parse(json);
            jo[FourthSwordField] = fourthSwordPlace();
            jo[ProgressiveField] = TrackerModel.ProgressiveItems;
            if (TrackerModel.ShuffleAnywhere)
                jo[ShuffleSpotsField] = new Newtonsoft.Json.Linq.JArray(TrackerModel.shuffleSlotBoxes.Select(b => $"{b.CellCurrent()},{b.PlayerHas().AsInt()},{b.ShuffleLevel}"));
            else if (TrackerModel.ShuffleMaps || TrackerModel.ShuffleTriforces)
                jo[ShuffleSlotsField] = new Newtonsoft.Json.Linq.JArray(Enumerable.Range(0, 27).Select(k => $"{(TrackerModel.shuffleSlotGot[k] ? 1 : 0)},{TrackerModel.shuffleSlotChosen[k]}"));
            return jo.ToString(Formatting.None);
        }
    }

    // ---- the 4th Sword's place and Progressive Items ----
    // Both travel in the Items message, with versions like the rest of it. The rule holds on the receiving side too:
    // the partner's 4th Sword turned on turns Progressive Items on here (SetFourthSwordLocation does that, and remembers it, so setting none
    // later turns it back off only if this run's 4th Sword turned it on and nothing changed since); the partner's Progressive Items turned on
    // turns ours on but never the 4th Sword; the partner's Progressive Items off is never taken (only the turn-on is synced).
    // In Legacy Mode none of it is sent or taken.
    public const string FourthSwordField = "FourthSwordPlace";   // 0 none, 1 the level 2 staircase, 2 the level 9 floor
    public const string ProgressiveField = "ProgressiveItemsOn";
    public static string fourthSwordKey => itemsFlagKey("fourthSword");
    public static string progressiveKey => itemsFlagKey("progressive");
    public static string level9TriforcesKey => itemsFlagKey("level9Triforces");

    /// Where the 4th Sword's item check is now, as SetFourthSwordLocation takes it.
    public static int fourthSwordPlace() => TrackerModel.level9FloorBoxEnabled ? 2 : TrackerModel.level2StairBoxEnabled ? 1 : 0;

    /// The user moved the 4th Sword (the Extras checkbox): send it now, not with the next poll. UI thread.
    public static void noteFourthSwordChanged()
    {
        if (TrackerModelOptions.isCurrentlyApplyingRemoteUpdate()) return;
        noteLocalEdit();
        sendItemsUpdate(TrackerModelOptions.CoopSyncOptions.MyConsoleId);   // (sends only when co-op is on and the run has started)
    }

    /// A partner's 4th Sword place and Progressive Items, from an Items message whose merge is m. Call it before the boxes are applied:
    /// a new place moves our slot's item, and the boxes then get the partner's items. Returns true when the place or Progressive Items changed
    /// here (then call finishFourthSwordFromSync after endMerge). UI thread, inside applyRemote.
    public static bool applyFourthSwordFromSync(Merge m, Newtonsoft.Json.Linq.JToken payload)
    {
        if (m == null || payload == null) return false;
        if (TrackerModel.LegacyMode)
        {
            // (the option is disabled in Legacy Mode: the partner's place and Progressive Items are not taken, and no version of them is kept)
            m.Taken.Remove(fourthSwordKey);
            m.Taken.Remove(progressiveKey);
            return false;
        }
        var changed = false;
        if (m.Takes(fourthSwordKey))
        {
            var t = payload[FourthSwordField];
            var place = t is Newtonsoft.Json.Linq.JValue v && v.Type == Newtonsoft.Json.Linq.JTokenType.Integer ? (long)v : -1;
            if (place < 0 || place > 2) m.Fail(fourthSwordKey);
            else if (place != fourthSwordPlace())
            {
                var l2 = TrackerModel.level2StairBox;
                var l9 = TrackerModel.GetDungeon(8).ExtraFloorBox;
                var before = new[] { (l2, l2.CellCurrent(), l2.PlayerHas()), (l9, l9.CellCurrent(), l9.PlayerHas()) };
                OverworldItemGridUI.SetFourthSwordLocation((int)place);   // (turns Progressive Items on with the 4th Sword, as a click here does)
                // a slot whose item the move changed, and that the message does not set: our edit, so the partner gets it
                // (their message may not know an item we marked there a moment ago)
                foreach (var (box, cell, has) in before)
                {
                    var k = boxKey(box);
                    if (k != null && !m.Takes(k) && (box.CellCurrent() != cell || !box.PlayerHas().Equals(has))) m.Displaced.Add(k);
                }
                changed = true;
                TrackerModelOptions.DebugConfig.Log($"[Sync] The partner put the 4th Sword at {(place == 2 ? "level 9" : place == 1 ? "level 2" : "none")}");
            }
        }
        if (m.Takes(progressiveKey))
        {
            var t = payload[ProgressiveField];
            if (!(t is Newtonsoft.Json.Linq.JValue v && v.Type == Newtonsoft.Json.Linq.JTokenType.Boolean)) m.Fail(progressiveKey);
            else if ((bool)v)
            {
                if (!TrackerModel.ProgressiveItems) { OverworldItemGridUI.TurnProgressiveItemsOnFromPartner(); changed = true; }
            }
            else if (TrackerModel.ProgressiveItems)
            {
                // (their Progressive Items off is not taken: ours stays on. Their version is kept, without the check that our value now matches,
                // so ours is not sent back to turn theirs on again)
                m.Theirs?.Remove(progressiveKey);
            }
        }
        return changed;
    }

    /// After a partner's Items message changed the 4th Sword or Progressive Items here (and endMerge ran): the screen is redrawn as after a click
    /// on the Extras checkbox. UI thread, inside applyRemote.
    public static void finishFourthSwordFromSync()
    {
        TrackerModel.recomputePlayerStateSummary();
        TrackerModel.recomputeMapStateSummary();
        TrackerModel.forceUpdate();
        OverworldItemGridUI.refreshUIAfterOverworldTemplateChange();
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
                noteLocalEdit();   // (the box gets its version when the Items message is built, see stamp)
            }
        }

        // Top-level boxes
        onEvent(TrackerModel.sword2Box.Changed, _ => markChanged(TrackerModel.sword2Box));
        onEvent(TrackerModel.ladderBox.Changed, _ => markChanged(TrackerModel.ladderBox));
        onEvent(TrackerModel.armosBox.Changed, _ => markChanged(TrackerModel.armosBox));
        onEvent(TrackerModel.sword3Box.Changed, _ => markChanged(TrackerModel.sword3Box));
        onEvent(TrackerModel.letterBox.Changed, _ => markChanged(TrackerModel.letterBox));
        onEvent(TrackerModel.sword1Box.Changed, _ => markChanged(TrackerModel.sword1Box));
        onEvent(TrackerModel.level2StairBox.Changed, _ => markChanged(TrackerModel.level2StairBox));   // (the 4th Sword's level 2 staircase slot)

        // Dungeon boxes, and each dungeon's extra floor item slot (4th Sword)
        for (int i = 0; i <= 8; i++)
        {
            var dungeon = TrackerModel.GetDungeon(i);
            foreach (var box in dungeon.Boxes.Concat(new[] { dungeon.ExtraFloorBox }))
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
                sendWholeState("Overworld");
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

    /// Sends one overworld tile as it is now, with its version. UI thread.
    public static void sendTile(int x, int y)
    {
        try
        {
            if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop() && startupBaselineDone)
            {
                stamp("Overworld");   // (a change of this tile gets its version here)
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

                var jsonPayload = withVersions("Overworld", JsonConvert.SerializeObject(update), new[] { tileKey(x, y) });
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





    public static Task sendDungeonTriforceUpdate(string myConsoleId)
    {
        sendWholeState("DungeonTriforce");
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

                if (!startupBaselineDone) return;

                try
                {
                    var jsonPayload = buildDungeonMapsJsonCached();
                    stamp("DungeonMaps", stateOfJson("DungeonMaps", jsonPayload));

                    // (sent when it has changes not sent yet, must go (a hello snapshot, or the partner lacks ours), or nothing was sent since a reset)
                    var dungeonMapsChanged = unsentEdits.Contains("DungeonMaps") || mustSend.Contains("DungeonMaps") || lastSentDungeonMapsPayload == "";
                    var versioned = dungeonMapsChanged ? withVersions("DungeonMaps", jsonPayload, null, snapshotPending.Contains("DungeonMaps")) : null;
                    var dungeonMapsSend = dungeonMapsChanged && shouldSendUpdate("DungeonMaps", versioned);
                    if (dungeonMapsChanged && !dungeonMapsSend && DateTime.UtcNow < autoMutedUntil)
                    {
                        // (an update dropped by the anti-spam mute used to be lost until the next edit: try again once the mute ends, on the UI thread
                        // since the debouncer is not thread safe. The mute is set by shouldSendUpdate above, so this must come after it.)
                        var wait = autoMutedUntil - DateTime.UtcNow + TimeSpan.FromMilliseconds(100);
                        Task.Delay(wait).ContinueWith(_ => Avalonia.Threading.Dispatcher.UIThread.Post(() => sendDungeonMapsUpdate(myConsoleId)));
                    }
                    if (dungeonMapsSend)
                    {
                        TrackerModelOptions.DebugConfig.Log($"[Sync] DungeonMaps update: {versioned}");
                        if (trySend("DungeonMaps", versioned))
                        {
                            lastSentDungeonMapsPayload = jsonPayload;
                            unsentEdits.Remove("DungeonMaps"); mustSend.Remove("DungeonMaps"); snapshotPending.Remove("DungeonMaps");
                        }
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
                    if (TrackerModelOptions.isCurrentlyApplyingRemoteUpdate())
                        return;   // a partner's label being applied: do not send it back
                    noteLocalEdit();
                    sendHiddenDungeonColorLabel(i);
                });
            }
        }
    }

    /// Sends dungeon i's color and label as they are now, with their version. UI thread (the label is changed by clicks).
    public static void sendHiddenDungeonColorLabel(int i)
    {
        try
        {
            if (!startupBaselineDone || !coopActive()) return;
            stamp("Items");   // (the label is part of the Items state: a change gets its version here)
            var dungeon = TrackerModel.GetDungeon(i);
            // (F#: `dict [...]`; Newtonsoft writes the keys in this order)
            var payload =
                new Dictionary<string, object>
                {
                    ["Model"] = "HiddenDungeonColorLabel",
                    ["Index"] = i,
                    ["Color"] = dungeon.Color,
                    ["LabelChar"] = dungeon.LabelChar,
                    ["Source"] = TrackerModelOptions.CoopSyncOptions.MyConsoleId
                };

            var jsonPayload = withVersions("Items", JsonConvert.SerializeObject(payload), new[] { labelKey(i) });

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
    }
    // (F#: the body is an `async { }` started with Async.StartImmediate; nothing in it awaits, so it runs right here)
    // isResend: our value sent again because the partner lacked it, not a new edit (the hello's state time stays)
    public static void sendDoorChangeUpdate(DungeonUI.DoorChangeInfo info, string myConsoleId, bool isResend = false)
    {
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => sendDoorChangeUpdate(info, myConsoleId, isResend)); return; }   // (the versions live on the UI thread)
        try
        {
            if (!isResend) noteLocalEdit();
            if (!isDoorSyncReady() || !startupBaselineDone)
            {
                TrackerModelOptions.DebugConfig.Log("[Sync] Door sync not ready, skipping DoorChange update");
                return;
            }
            stamp("DungeonMaps");   // (the door's change gets its version here)
            var jsonPayload = withVersions("DungeonMaps", JsonConvert.SerializeObject(info, new DoorStateAsUnionCaseConverter()), new[] { doorKey(info.Level, info.X, info.Y, info.IsHorizontal) });
            if (shouldSend("DoorChange", jsonPayload))
            {
                TrackerModelOptions.DebugConfig.Log($"[Sync] Sending DoorChange update: {jsonPayload}");
                if (trySend("DoorChange", jsonPayload))
                    SyncManager.MarkLastSentDoorChange(jsonPayload);
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
        if (!Dispatcher.UIThread.CheckAccess()) { Dispatcher.UIThread.Post(() => sendRoomChangeUpdate(level, x, y, room, myConsoleId, isResend)); return; }   // (the versions live on the UI thread)
        try
        {
            //let shouldSuppress =
            //    System.Windows.Application.Current.Dispatcher.Invoke(fun () ->
            //        DungeonPopups.suppressRoomSyncTemporarily
            //    )

            //if not shouldSuppress && level <> 0 then
            if (level != 0)
            {
                if (!isResend && !(DungeonPopups.suppressRoomSyncTemporarily))
                    noteLocalEdit();
                if (DungeonPopups.suppressRoomSyncTemporarily || !startupBaselineDone || !coopActive()) return;
                stamp("DungeonMaps");   // (the room's change gets its version here)
                var json = withVersions("DungeonMaps", serializeRoomChange(level, x, y, room), new[] { roomKey(level, x, y) });
                if (shouldSend("RoomChange", json) && trySend("RoomChange", json))
                    TrackerModelOptions.DebugConfig.Log($"[Sync] Sent RoomChange: {json}");
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
