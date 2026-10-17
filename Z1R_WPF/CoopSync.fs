module CoopSync

open Newtonsoft.Json
open System.Threading.Tasks
open Z1R_Sync
open System.Security.Cryptography
open System.Text
open System
open System.Collections.Concurrent
open SyncOriginTracker

let mutable lastSentPlayerProgressJson = ""
let mutable lastSentStartingItemsJson = ""
let mutable lastSentItemsJson = ""
let mutable lastSentOverworldJson = ""
let mutable lastSentDungeonJson = ""
let mutable lastReceivedDungeonJson = ""
let mutable lastSentTriforceJson = ""
let mutable lastReceivedItemsHash: string option = None
let mutable lastReceivedHashes = System.Collections.Concurrent.ConcurrentDictionary<string, string>()
let mutable lastAppliedHashes = System.Collections.Concurrent.ConcurrentDictionary<string, string>()
let mutable lastSentHashes = ConcurrentDictionary<string, string>()
let mutable lastSentBlockersJson = ""
let isDoorSyncReady () =
    TrackerModelOptions.CoopSyncOptions.GetEnableCoop() &&
    TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption.IsSome

let dungeonMapsSyncOrigin = SyncOriginTracker.SyncOriginTracker()
let private hashRetentionLimit = 20
let private recentReceivedHashes = ConcurrentDictionary<string, ResizeArray<string>>()

let private MAX_ENTRIES = 500
let private lastAppliedRoomPayloads = System.Collections.Concurrent.ConcurrentDictionary<string, string>()
let private lastAppliedDoorPayloads = System.Collections.Concurrent.ConcurrentDictionary<string, string>()
let private lastTimestamps = ConcurrentDictionary<string, int64>()

let private pruneIfNeeded (dict: ConcurrentDictionary<string, 'T>) name =
    if dict.Count > MAX_ENTRIES then
        let oldestKeys =
            lastTimestamps
            |> Seq.sortBy (fun kvp -> kvp.Value)
            |> Seq.map (fun kvp -> kvp.Key)
            |> Seq.filter dict.ContainsKey
            |> Seq.truncate (dict.Count - MAX_ENTRIES)
            |> Seq.toList

        for key in oldestKeys do
            let removed = dict.TryRemove(key) |> fst
            if removed then
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Pruned stale entry %s from %s" key name)

let syncBurstTimestamps = ConcurrentQueue<DateTime>()
let syncBurstWindowMs = 2000
let syncBurstLimit = 5
let syncPauseDurationMs = 1000
let mutable autoMutedUntil = DateTime.MinValue

type RoomChangePayload = {
    Level: int
    X: int
    Y: int
    IsComplete: bool
    RoomType: string
    MonsterDetail: string
    FloorDropDetail: string
    FloorDropAppearsBright: bool
}

type OverworldTileUpdate = {
    X: int
    Y: int
    MapTileValue: int
    ExtraData: int
    CircleValue: int
}


let serializeRoomChange (level: int) (x: int) (y: int) (room: Z1R_Tracker.Models.CDungeonRoomState) : string =
    let payload = {
        Level = level
        X = x
        Y = y
        IsComplete = room.IsComplete
        RoomType = room.RoomType.ToString()
        MonsterDetail = room.MonsterDetail.ToString()
        FloorDropDetail = room.FloorDropDetail.ToString()
        FloorDropAppearsBright = room.FloorDropAppearsBright
    }
    JsonConvert.SerializeObject(payload)



type DoorChangePayload = {
    Level: int
    X: int
    Y: int
    IsHorizontal: bool
    NewState: {| Case: string |} // will contain "UNKNOWN", "NO", "YES", etc.
}



let computeHash (s: string) =
    using (SHA256.Create()) (fun sha ->
        s |> Encoding.UTF8.GetBytes
          |> sha.ComputeHash
          |> Convert.ToBase64String
    )

let alreadyReceived (messageType: string) (payloadJson: string) =
    let hash = computeHash payloadJson

    let queue =
        recentReceivedHashes.GetOrAdd(messageType, fun _ -> ResizeArray<string>())

    lock queue (fun () ->
        if queue.Contains(hash) then
            true
        else
            queue.Add(hash)
            // Trim oldest if over limit
            if queue.Count > hashRetentionLimit then
                queue.RemoveAt(0)
            false
    )

let shouldSend (messageType: string) (payloadJson: string) =
    let newHash = computeHash payloadJson
    match lastSentHashes.TryGetValue(messageType) with
    | true, oldHash when oldHash = newHash -> false  // same as last time, skip it
    | _ ->
        lastSentHashes.[messageType] <- newHash
        true

//let lastAppliedHashes = System.Collections.Concurrent.ConcurrentDictionary<string, string>()
//let lastTimestamps = System.Collections.Concurrent.ConcurrentDictionary<string, int64>()

let private lastAppliedRoomTimestamps = System.Collections.Generic.Dictionary<string, int64>()
let private lastAppliedDoorTimestamps = System.Collections.Generic.Dictionary<string, int64>()

let private makeRoomKey level x y = sprintf "room-%d-%d-%d" level x y
let private makeDoorKey level x y isH = sprintf "door-%d-%d-%d-%b" level x y isH

let private lastSentTimestamps = ConcurrentDictionary<string, int64>()

/// Get the current timestamp in ms
let private now() = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()

/// Record a send event for the given key
let recordSend key =
    lastSentTimestamps.[key] <- now()

/// Should a remote update be ignored due to recent local send?
let isRecentlySentByUs key maxAgeMs =
    match lastSentTimestamps.TryGetValue(key) with
    | true, ts -> now() - ts < int64 maxAgeMs
    | _ -> false

/// Optional cleanup
let purgeOldEntries maxAgeMs =
    let threshold = now() - int64 maxAgeMs
    for kvp in lastSentTimestamps do
        if kvp.Value < threshold then
            lastSentTimestamps.TryRemove(kvp.Key) |> ignore


let recordRoomTimestamp level x y timestamp =
    lastAppliedRoomTimestamps.[makeRoomKey level x y] <- timestamp

let recordDoorTimestamp level x y isH timestamp =
    lastAppliedDoorTimestamps.[makeDoorKey level x y isH] <- timestamp

let isRoomChangeFresh level x y incomingTs =
    match lastAppliedRoomTimestamps.TryGetValue(makeRoomKey level x y) with
    | true, ts -> incomingTs > ts
    | _ -> true

let isDoorChangeFresh level x y isH incomingTs =
    match lastAppliedDoorTimestamps.TryGetValue(makeDoorKey level x y isH) with
    | true, ts -> incomingTs > ts
    | _ -> true

let shouldApplyUpdate (messageType: string) (payloadJson: string) (timestamp: int64) : bool =
    let newHash = computeHash payloadJson

    let updateHashAndTimestamp key =
        lastAppliedHashes.[messageType] <- newHash
        lastTimestamps.[key] <- timestamp

    match messageType with
    | "RoomChange" ->
        try
            let r = JsonConvert.DeserializeObject<RoomChangePayload>(payloadJson)
            let key = sprintf "room-%d-%d-%d" r.Level r.X r.Y
            match lastAppliedRoomPayloads.TryGetValue(key) with
            | true, oldPayload when oldPayload = payloadJson ->
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped RoomChange — same payload for %s" key)
                false
            | _ ->
                match lastTimestamps.TryGetValue(key) with
                | true, prevTs when timestamp <= prevTs ->
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped RoomChange — stale timestamp (%d <= %d)" timestamp prevTs)
                    false
                | _ ->
                    lastAppliedRoomPayloads.[key] <- payloadJson
                    updateHashAndTimestamp key
                    pruneIfNeeded lastAppliedRoomPayloads "RoomPayloads"
                    true
        with _ ->
            TrackerModelOptions.DebugConfig.Log("[Sync] RoomChange payload malformed — applying anyway")
            true

    | "DoorChange" ->
        try
            let d = JsonConvert.DeserializeObject<DoorChangePayload>(payloadJson)
            let key = sprintf "door-%d-%d-%d-%b" d.Level d.X d.Y d.IsHorizontal
            match lastAppliedDoorPayloads.TryGetValue(key) with
            | true, oldPayload when oldPayload = payloadJson ->
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped DoorChange — same payload for %s" key)
                false
            | _ ->
                match lastTimestamps.TryGetValue(key) with
                | true, prevTs when timestamp <= prevTs ->
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped DoorChange — stale timestamp (%d <= %d)" timestamp prevTs)
                    false
                | _ ->
                    lastAppliedDoorPayloads.[key] <- payloadJson
                    updateHashAndTimestamp key
                    pruneIfNeeded lastAppliedDoorPayloads "DoorPayloads"
                    true
        with _ ->
            TrackerModelOptions.DebugConfig.Log("[Sync] DoorChange payload malformed — applying anyway")
            true

    | _ ->
        let key = messageType
        match lastAppliedHashes.TryGetValue(messageType) with
        | true, oldHash when oldHash = newHash ->
            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped %s update — duplicate hash" messageType)
            false
        | _ ->
            match lastTimestamps.TryGetValue(key) with
            | true, prevTs when timestamp <= prevTs ->
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped %s — stale timestamp (%d <= %d)" messageType timestamp prevTs)
                false
            | _ ->
                updateHashAndTimestamp key
                pruneIfNeeded lastAppliedHashes "Hashes"
                true


let shouldSendUpdate (messageType: string) (payloadJson: string) =
    let now = DateTime.UtcNow

    if now < autoMutedUntil then
        TrackerModelOptions.DebugConfig.Log("[Sync] Auto-muted — update skipped")
        false
    else
        syncBurstTimestamps.Enqueue(now)

        let mutable peekValue = DateTime.MinValue
        while syncBurstTimestamps.TryPeek(&peekValue) && (now - peekValue).TotalMilliseconds > float syncBurstWindowMs do
            let mutable _ = Unchecked.defaultof<DateTime>
            let mutable dummy = Unchecked.defaultof<DateTime>
            syncBurstTimestamps.TryDequeue(&dummy) |> ignore
            ()

        if syncBurstTimestamps.Count >= syncBurstLimit then
            autoMutedUntil <- now.AddMilliseconds(float syncPauseDurationMs)
            TrackerModelOptions.DebugConfig.Log("[Sync] Auto-muted due to excessive chatter")
            false
        else
            shouldSend messageType payloadJson

[<AllowNullLiteral>]
type DungeonsTriforceState() =
    member val Triforces: bool[] = null with get, set


let sendPlayerProgressUpdate (myConsoleId: string) =
    async {
        try
            let payload = SaveAndLoad.PlayerProgressAndTakeAnyHeartsModel.Create()
            let jsonPayload = JsonConvert.SerializeObject(payload)
            if jsonPayload <> lastSentPlayerProgressJson then
                lastSentPlayerProgressJson <- jsonPayload
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Sending PlayerProgress update: %s" jsonPayload)
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop()) then
                    SyncManager.EnqueueSync("PlayerProgress", jsonPayload, myConsoleId)
                else
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Coop sync is disabled, not sending PlayerProgress update")
        with ex ->
            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send PlayerProgress update: %s" ex.Message)
    }

let mutable isApplyingPlayerProgressFromSync = false

let subscribeToPlayerProgressChanges(myConsoleId: string) =
    let lastChanged = TrackerModel.LastChangedTime()

    let markChanged () =
        if not isApplyingPlayerProgressFromSync then
            lastChanged.SetNow()

    // Listen to the TakeAnyHeart array changes
    TrackerModel.playerProgressAndTakeAnyHearts.TakeAnyHeartChanged.Add(fun _ -> markChanged())

    // Listen to all BoolProperties
    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBoomBook.Changed.Add(fun _ -> markChanged())
    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodSword.Changed.Add(fun _ -> markChanged())
    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasWoodArrow.Changed.Add(fun _ -> markChanged())
    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueRing.Changed.Add(fun _ -> markChanged())
    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBlueCandle.Changed.Add(fun _ -> markChanged())
    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasMagicalSword.Changed.Add(fun _ -> markChanged())
    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasDefeatedGanon.Changed.Add(fun _ -> markChanged())
    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasRescuedZelda.Changed.Add(fun _ -> markChanged())
    TrackerModel.playerProgressAndTakeAnyHearts.PlayerHasBombs.Changed.Add(fun _ -> markChanged())

    let mutable lastSentTime = System.DateTime.MinValue

    let rec loop () =
        async {
            do! Async.Sleep(500)
            let currentTime = lastChanged.Time
            if currentTime > lastSentTime then
                lastSentTime <- currentTime
                do! sendPlayerProgressUpdate myConsoleId
            return! loop ()
        }

    Async.StartImmediate(loop ())

let sendStartingItemsAndExtrasUpdate (myConsoleId: string) =
    async {
        try
            let payload = SaveAndLoad.StartingItemsAndExtrasModel.Create()
            let jsonPayload = JsonConvert.SerializeObject(payload)
            if jsonPayload <> lastSentStartingItemsJson then
                lastSentStartingItemsJson <- jsonPayload
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Sending StartingItemsAndExtras update: %s" jsonPayload)
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop()) then
                    SyncManager.EnqueueSync("StartingItems", jsonPayload, myConsoleId)
                else
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Coop sync is disabled, not sending StartingItemsAndExtras update")
        with ex ->
            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send StartingItemsAndExtras update: %s" ex.Message)
    }

let subscribeToStartingItemsAndExtrasChanges(myConsoleId: string) =
    let sendUpdate() =
        async {
            do! sendStartingItemsAndExtrasUpdate myConsoleId
        } |> Async.StartImmediate

    TrackerModel.startingItemsAndExtras.PlayerHasWhiteSword.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasMagicalSword.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasSilverArrow.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasBow.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasWand.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasRedCandle.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasBoomerang.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasMagicBoomerang.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasRedRing.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasPowerBracelet.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasLadder.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasRaft.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasRecorder.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasAnyKey.Changed.Add(fun _ -> sendUpdate())
    TrackerModel.startingItemsAndExtras.PlayerHasBook.Changed.Add(fun _ -> sendUpdate())

let sendBlockersUpdate (myConsoleId: string) =
    async {
        try
            let payload =
                Array.init 8 (fun i ->
                    Array.init TrackerModel.DungeonBlockersContainer.MAX_BLOCKERS_PER_DUNGEON (fun j ->
                        let jsonStr = TrackerModel.DungeonBlockersContainer.AsJsonString(i, j)
                        JsonConvert.DeserializeObject<SaveAndLoad.Blocker>(jsonStr)
                    )
                )
            let jsonPayload = JsonConvert.SerializeObject(payload)
            if shouldSend "Blockers" jsonPayload then
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Sending Blockers update: %s" jsonPayload)
                if TrackerModelOptions.CoopSyncOptions.GetEnableCoop() then
                    SyncManager.EnqueueSync("Blockers", jsonPayload, myConsoleId)
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending Blockers update")
            else
                TrackerModelOptions.DebugConfig.Log("[Sync] Skipped sending Blockers update — no change")
        with ex ->
            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send Blockers update: %s" ex.Message)
    }

let subscribeToBlockersChanges (myConsoleId: string) =
    TrackerModel.DungeonBlockersContainer.AnyBlockerChanged.Add(fun _ ->
        async {
            do! sendBlockersUpdate myConsoleId
        } |> Async.StartImmediate
    )



let toSerializableBox (b: TrackerModel.Box) =
    let box = new SaveAndLoad.Box()
    box.CellCurrent <- b.CellCurrent()
    box.PlayerHas <- b.PlayerHas().AsInt()
    box






let sendItemsUpdate (myConsoleId: string) =
    async {
        try
            let model = new SaveAndLoad.Items()
            model.HiddenDungeonNumbers <- TrackerModel.IsHiddenDungeonNumbers()
            model.SecondQuestDungeons <- TrackerModel.IsSecondQuestDungeons
            model.WhiteSwordBox <- toSerializableBox TrackerModel.sword2Box
            model.LadderBox <- toSerializableBox TrackerModel.ladderBox
            model.ArmosBox <- toSerializableBox TrackerModel.armosBox

            model.Dungeons <- 
                [| for i in 0 .. 8 ->
                    let d = new SaveAndLoad.Dungeon()
                    let trackerDungeon = TrackerModel.GetDungeon(i)
                    
                    d.Triforce <- false // EXCLUDE this value — syncing Triforce separately
                    d.Color <- trackerDungeon.Color
                    d.LabelChar <- trackerDungeon.LabelChar.ToString()
                    d.PlayerHasMap <- trackerDungeon.PlayerHasMapOfThisDungeon
                    d.Boxes <- 
                        [| for box in trackerDungeon.Boxes -> toSerializableBox box |]
                    d |]

            let jsonPayload = JsonConvert.SerializeObject(model)

            if shouldSend "Items" jsonPayload then
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Sending Items update (excluding Triforce): %s" jsonPayload)
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop()) then
                    SyncManager.EnqueueSync("Items", jsonPayload, myConsoleId)
                else
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Coop sync is disabled, not sending Items update")
            else
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped sending Items update — no change")
        with ex ->
            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send Items update: %s" ex.Message)
    }

let mutable isApplyingItemsFromSync = false

let subscribeToItemsChanges(myConsoleId: string) =
    // Shared timestamp that all item-related changes update
    let lastChanged = TrackerModel.LastChangedTime()

    // Helper to mark the model as changed — suppressed during remote apply to prevent echo
    let markChanged () =
        if not isApplyingItemsFromSync then
            lastChanged.SetNow()

    // Top-level boxes
    TrackerModel.sword2Box.Changed.Add(fun _ -> markChanged())
    TrackerModel.ladderBox.Changed.Add(fun _ -> markChanged())
    TrackerModel.armosBox.Changed.Add(fun _ -> markChanged())

    // Dungeon boxes
    for i = 0 to 8 do
        let dungeon = TrackerModel.GetDungeon(i)
        for box in dungeon.Boxes do
            box.Changed.Add(fun _ -> markChanged())

    // Polling loop to debounce updates
    let mutable lastSentTime = System.DateTime.MinValue

    let rec loop () =
        async {
            do! Async.Sleep(500)
            let currentTime = lastChanged.Time
            if currentTime > lastSentTime then
                lastSentTime <- currentTime
                do! sendItemsUpdate myConsoleId
            return! loop ()
        }

    Async.StartImmediate(loop ())

let createSerializableOverworldModel () =
    let model = SaveAndLoad.Overworld()
    model.MirrorOverworld <- TrackerModel.MirrorOverworld
    model.StartIconX <- TrackerModel.startIconX
    model.StartIconY <- TrackerModel.startIconY
    model.CustomWaypointX <- TrackerModel.customWaypointX
    model.CustomWaypointY <- TrackerModel.customWaypointY

    let map = Array.zeroCreate (16 * 8 * 3)
    for j = 0 to 7 do
        for i = 0 to 15 do
            let index = (j * 16 + i) * 3
            let cur = TrackerModel.overworldMapMarks.[i,j].Current()
            let ed =
                if cur >= 0 && cur <= TrackerModel.MapSquareChoiceDomainHelper.DARK_X then
                    try
                        // Shop tiles (items) all store extra data under the SHOP key regardless of which item is first
                        let key = if TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur) then TrackerModel.MapSquareChoiceDomainHelper.SHOP else cur
                        TrackerModel.getOverworldMapExtraData(i,j,key)
                    with _ -> 0
                else
                    0
            let circ = TrackerModel.overworldMapCircles.[i,j]
            map.[index] <- cur
            map.[index + 1] <- ed
            map.[index + 2] <- circ
    model.Map <- map

    model



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
let mutable debounceOverworldSend: System.Threading.CancellationTokenSource option = None
let overworldDebouncer = Debouncer.Debouncer(200)

let sendOverworldUpdateDebounced (myConsoleId: string) =
    overworldDebouncer.Trigger(fun() ->
        async {
            try
                let model = createSerializableOverworldModel()
                let jsonPayload = JsonConvert.SerializeObject(model)
                if jsonPayload <> lastSentOverworldJson then
                    lastSentOverworldJson <- jsonPayload
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Sending Overworld update: %s" jsonPayload)
                    if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop()) then
                        SyncManager.EnqueueSync("Overworld", jsonPayload, myConsoleId)
                    else
                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Coop sync is disabled, not sending Overworld update")
            with ex ->
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send Overworld update: %s" ex.Message)
        }
        |> Async.Start
    )



let subscribeToOverworldChanges (myConsoleId: string) =
    let mutable lastSentTime = System.DateTime.MinValue

    let rec loop () =
        async {
            do! Async.Sleep(500)  // check every 500ms
            let currentTime = TrackerModel.mapLastChangedTime.Time
            if currentTime.Ticks > lastSentTime.Ticks then
                lastSentTime <- currentTime.AddMilliseconds(1.0)
                sendOverworldUpdateDebounced myConsoleId
            return! loop ()
        }
    Async.StartImmediate(loop ())

let subscribeToOverworldTileChanges (myConsoleId: string) =
    for x in 0 .. 15 do
        for y in 0 .. 7 do
            TrackerModel.overworldMapMarks.[x, y].Changed.Add(fun _ ->
                async {
                    try
                        if TrackerModelOptions.CoopSyncOptions.GetEnableCoop() then
                            let cur = TrackerModel.overworldMapMarks.[x, y].Current()
                            // Shop tiles store extra data (item2, item3) under SHOP key regardless of which item is first
                            let edKey = if TrackerModel.MapSquareChoiceDomainHelper.IsItem(cur) then TrackerModel.MapSquareChoiceDomainHelper.SHOP else cur
                            let ed = TrackerModel.getOverworldMapExtraData(x, y, edKey)
                            let circ = TrackerModel.overworldMapCircles.[x, y]

                            let update = {
                                X = x
                                Y = y
                                MapTileValue = cur
                                ExtraData = ed
                                CircleValue = circ
                            }

                            let jsonPayload = JsonConvert.SerializeObject(update)
                            if shouldSend "OverworldTile" jsonPayload then
                                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Sending OverworldTile (%d,%d): %s" x y jsonPayload)
                                SyncManager.EnqueueSync("OverworldTile", jsonPayload, myConsoleId)
                    with ex ->
                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send OverworldTile (%d,%d): %s" x y ex.Message)
                } |> Async.Start
            )


(*let createSerializableDungeonArray () =
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
            box.Changed.Add(fun _ -> sendUpdate())*)


let sendDungeonTriforceUpdate (myConsoleId: string) =
    async {
        try
            let model = DungeonsTriforceState()
            model.Triforces <- [| for i in 0 .. 8 -> TrackerModel.GetDungeon(i).PlayerHasTriforce() |]
            let json = JsonConvert.SerializeObject(model)
            if shouldSend "DungeonTriforce" json then
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Sending DungeonTriforce update: %s" json)
                if (TrackerModelOptions.CoopSyncOptions.GetEnableCoop()) then
                    SyncManager.EnqueueSync("DungeonTriforce", json, myConsoleId)
                else
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Coop sync is disabled, not sending DungeonTriforce update")
            else
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Skipped sending DungeonTriforce update — no change")
        with ex ->
            printf "[Sync] Failed to send DungeonTriforce update: %s" ex.Message
    }

let mutable isApplyingTriforceFromSync = false

let subscribeToDungeonTriforceChanges (myConsoleId: string) =
    for i in 0 .. 8 do
        TrackerModel.GetDungeon(i).PlayerHasTriforceChanged.Add(fun _ ->
            if not isApplyingTriforceFromSync && TrackerModelOptions.CoopSyncOptions.GetEnableCoop() then
                sendDungeonTriforceUpdate myConsoleId |> Async.StartImmediate
        )

let transposeHorizontalDoors (input: int[][]) : int[][] =
    if isNull input then
        [||]
    else
        [| for i in 0 .. 7 -> [| for j in 0 .. 6 -> input.[j].[i] |] |]

let transposeVerticalDoors (input: int[][]) : int[][] =
    if isNull input then
        [||]
    else
        [| for i in 0 .. 6 -> [| for j in 0 .. 7 -> input.[j].[i] |] |]

let transposeRoomStates (input: DungeonSaveAndLoad.DungeonRoomModel[][]) : DungeonSaveAndLoad.DungeonRoomModel[][] =
    if isNull input then
        [||]
    else
        [| for i in 0 .. 7 -> [| for j in 0 .. 7 -> input.[j].[i] |] |]

let transposeRoomIsCircled (input: bool[][]) : bool[][] =
    if isNull input then [||]
    else [| for i in 0 .. 7 -> [| for j in 0 .. 7 -> input.[j].[i] |] |]

let cloneAndFixDungeonModel (dm: DungeonSaveAndLoad.DungeonModel) : DungeonSaveAndLoad.DungeonModel =
    let newDM = new DungeonSaveAndLoad.DungeonModel()
    newDM.HorizontalDoors <- transposeHorizontalDoors dm.HorizontalDoors
    newDM.VerticalDoors <- transposeVerticalDoors dm.VerticalDoors
    // importFunctions reads RoomIsCircled as [j][i], so pre-transpose it to match (same as RoomStates)
    newDM.RoomIsCircled <- transposeRoomIsCircled dm.RoomIsCircled
    newDM.RoomStates <- transposeRoomStates dm.RoomStates
    newDM.VanillaMapOverlay <- dm.VanillaMapOverlay
    newDM

// Create a mutable to debounce updates
let mutable lastDungeonMapsPayload = ""
let dungeonMapsDebouncer = Debouncer.Debouncer(200)
let mutable lastSentDungeonMapsPayload = ""




let sendDungeonMapsUpdate (myConsoleId: string) =
    dungeonMapsDebouncer.Trigger(fun () ->
        async {
            if TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption.IsNone then
                TrackerModelOptions.DebugConfig.Log("[Sync] Skipping DungeonMaps update: TrackerModel not yet initialized.")
                return ()

            try
                let dungeonModels =
                    [|
                        for i = 0 to 8 do
                            let exportFunction = DungeonUI.exportFunctionsLarge.[i]
                            let dm = exportFunction()
                            yield cloneAndFixDungeonModel dm
                    |]

                let jsonPayload = JsonConvert.SerializeObject(dungeonModels, Formatting.None)

                if jsonPayload <> lastSentDungeonMapsPayload && shouldSendUpdate "DungeonMaps" jsonPayload then
                    lastSentDungeonMapsPayload <- jsonPayload
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] DungeonMaps update: %s" jsonPayload)

                    if TrackerModelOptions.CoopSyncOptions.GetEnableCoop() then
                        SyncManager.EnqueueSync("DungeonMaps", jsonPayload, myConsoleId)
                    else
                        TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending DungeonMaps update")
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] Skipped DungeonMaps update — no change")
            with ex ->
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send DungeonMaps update: %s" ex.Message)
        } |> Async.Start
    )

let mutable lastSentTime = System.DateTime.MinValue

let subscribeToDungeonMapsChanges (myConsoleId: string) =
    let rec loop () =
        async {
            do! Async.Sleep(150)
            let currentChangeTime = TrackerModel.dungeonRoomModelChanged.Time
            if currentChangeTime > lastSentTime && not dungeonMapsSyncOrigin.IsApplyingSync then
                lastSentTime <- DateTime.Now
                sendDungeonMapsUpdate myConsoleId
            return! loop ()
        }
    Async.StartImmediate(loop())

// Create a mutable to debounce updates
let mutable lastHiddenDungeonColorLabelPayload = ""
let hiddenDungeonColorLabelDebouncer = Debouncer.Debouncer(200)

let subscribeToHiddenDungeonColorLabelChanges (myConsoleId: string) =
    if TrackerModel.IsHiddenDungeonNumbers()
    && TrackerModelOptions.CoopSyncOptions.GetEnableCoop() then
        for i = 0 to 8 do
            let dungeon = TrackerModel.GetDungeon(i)
            dungeon.HiddenDungeonColorOrLabelChanged.Add(fun (color, labelChar) ->
                async {
                    try
                        let payload =
                            dict [
                                "Model", box "HiddenDungeonColorLabel"
                                "Index", box i
                                "Color", box color
                                "LabelChar", box labelChar
                                "Source", box TrackerModelOptions.CoopSyncOptions.MyConsoleId
                            ]

                        let jsonPayload = JsonConvert.SerializeObject(payload)

                        if shouldSend "HiddenDungeonColorLabel" jsonPayload then
                            hiddenDungeonColorLabelDebouncer.Trigger(fun () ->
                                async {
                                    if jsonPayload <> lastHiddenDungeonColorLabelPayload then
                                        lastHiddenDungeonColorLabelPayload <- jsonPayload
                                        SyncManager.EnqueueSync("HiddenDungeonColorLabel", jsonPayload, myConsoleId)
                                } |> Async.Start
                            )
                    with ex ->
                        TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send HiddenDungeonColorLabel update: %s" ex.Message)
                } |> Async.StartImmediate
            )
let private roomChangeDebouncers = System.Collections.Concurrent.ConcurrentDictionary<string, Debouncer.Debouncer>()
let private doorChangeDebouncers = System.Collections.Concurrent.ConcurrentDictionary<string, Debouncer.Debouncer>()

let private roomKey (level: int) (x: int) (y: int) =
    sprintf "room-%d-%d-%d" level x y

let private doorKey (level: int) (x: int) (y: int) (isHorizontal: bool) =
    sprintf "door-%d-%d-%d-%b" level x y isHorizontal


let sendDoorChangeUpdate (info: DungeonUI.DoorChangeInfo) (myConsoleId: string) =
    let key = sprintf "DoorChange_%d_%d_%d_%b" info.Level info.X info.Y info.IsHorizontal
    Async.StartImmediate(async {
        try
            let jsonPayload = JsonConvert.SerializeObject(info)
            if shouldSend "DoorChange" jsonPayload then
                TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Sending DoorChange update: %s" jsonPayload)
                SyncManager.MarkLastSentDoorChange(jsonPayload)
                recordSend(key)

                let (doorSyncReady, coopEnabled) =
                    System.Windows.Application.Current.Dispatcher.Invoke(fun () ->
                        (isDoorSyncReady(), TrackerModelOptions.CoopSyncOptions.GetEnableCoop())
                    )

                if doorSyncReady then
                    if coopEnabled then
                        SyncManager.EnqueueSync("DoorChange", jsonPayload, myConsoleId)
                    else
                        TrackerModelOptions.DebugConfig.Log("[Sync] Coop sync is disabled, not sending DoorChange update")
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] Door sync not ready, skipping DoorChange update")
            else
                TrackerModelOptions.DebugConfig.Log("[Sync] Skipped sending DoorChange update — no change")
        with ex ->
            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send DoorChange update: %s" ex.Message)
    })

    //)



let mutable isApplyingRoomFromSync = false

let subscribeToDoorChanges (myConsoleId: string) =
    System.Diagnostics.Debug.WriteLine("[Sync] Subscribing to door changes...")
    DungeonUI.doorChangedEvent.Publish.Add(fun info ->
        if not TrackerModelOptions.CoopSyncOptions.IsBulkDoorInit && not isApplyingRoomFromSync then
            async {
                sendDoorChangeUpdate info myConsoleId
            } |> Async.StartImmediate
    )

let sendRoomChangeUpdate (level: int) (x: int) (y: int) (room: Z1R_Tracker.Models.CDungeonRoomState) (myConsoleId: string) =
    let key = sprintf "DungeonRoom_%d_%d_%d" level x y
    Async.StartImmediate(async {
        try
            //let shouldSuppress =
            //    System.Windows.Application.Current.Dispatcher.Invoke(fun () ->
            //        DungeonPopups.suppressRoomSyncTemporarily
            //    )

            //if not shouldSuppress && level <> 0 then
            if  level <> 0 then
                let json = serializeRoomChange level x y room
                if TrackerModelOptions.CoopSyncOptions.GetEnableCoop() && shouldSend "RoomChange" json && not (DungeonPopups.suppressRoomSyncTemporarily) then
                    TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Sending RoomChange: %s" json)
                    recordSend(key)
                    SyncManager.EnqueueSync("RoomChange", json, myConsoleId)
                else
                    TrackerModelOptions.DebugConfig.Log("[Sync] RoomChange not sent — coop disabled or not needed")
            else
                TrackerModelOptions.DebugConfig.Log("[Sync] Room sync suppressed or invalid level — skipping send")
        with ex ->
            TrackerModelOptions.DebugConfig.Log(sprintf "[Sync] Failed to send RoomChange update: %s" ex.Message)
    })

    //)





let subscribeToRoomChanges (myConsoleId: string) =
    printfn "[RoomSync] Starting to subscribe to room changes"

    Z1R_Tracker.Models.RoomSyncBridge.OnRoomChanged <-
        Action<int, int, int, Z1R_Tracker.Models.CDungeonRoomState>(fun level x y room ->
            if not isApplyingRoomFromSync && not (DungeonPopups.suppressRoomSyncTemporarily) then
                Async.StartImmediate(
                    async {
                        try
                            TrackerModelOptions.DebugConfig.Log(sprintf "[RoomSync] OnRoomChanged triggered for L%d (%d,%d) - ID %O" level x y (room.DebugId.ToString()))
                            sendRoomChangeUpdate level x y room myConsoleId
                        with ex ->
                            TrackerModelOptions.DebugConfig.Log(sprintf "[RoomSync] ERROR in RoomChanged handler: %s" ex.Message)
                    }
                )
        )

let handleRoomChange (json: string) =
    try
        let data = JsonConvert.DeserializeObject<RoomChangePayload>(json)
        TrackerModelOptions.DebugConfig.Log(sprintf "[RoomSync] Received RoomChange for L%d (%d,%d): %s" data.Level data.X data.Y json)

        if data.Level < 1 || data.Level > 9 || data.X < 0 || data.X > 7 || data.Y < 0 || data.Y > 7 then
            TrackerModelOptions.DebugConfig.Log("[RoomSync] Ignored RoomChange — invalid coordinates")
        else
            Z1R_Tracker.Models.RoomSyncBridge.ApplyRoomChangeFromSync(
                data.Level, data.X, data.Y,
                data.IsComplete,
                data.RoomType,
                data.MonsterDetail,
                data.FloorDropDetail,
                data.FloorDropAppearsBright
            )
    with ex ->
        TrackerModelOptions.DebugConfig.Log(sprintf "[RoomSync] ERROR handling RoomChange: %s" ex.Message)


