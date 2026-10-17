module DungeonModelSync

open SaveAndLoad
open DungeonRoomState
open DungeonSaveAndLoad
open Newtonsoft.Json
open Newtonsoft.Json.Linq

// Helper: Compare two DungeonRoomModels
let shouldUpdateRoom (current: DungeonRoomModel) (incoming: DungeonRoomModel) =
    current.IsCompleted <> incoming.IsCompleted ||
    current.RoomType <> incoming.RoomType ||
    current.MonsterDetail <> incoming.MonsterDetail ||
    current.FloorDropDetail <> incoming.FloorDropDetail ||
    current.FloorDropShouldAppearBright <> incoming.FloorDropShouldAppearBright

// Helper: Apply properties from one DungeonRoomModel to another
let applyRoomModel (current: DungeonRoomModel) (incoming: DungeonRoomModel) =
    current.IsCompleted <- incoming.IsCompleted
    current.RoomType <- incoming.RoomType
    current.MonsterDetail <- incoming.MonsterDetail
    current.FloorDropDetail <- incoming.FloorDropDetail
    current.FloorDropShouldAppearBright <- incoming.FloorDropShouldAppearBright

// Main function to apply a single DungeonModel update
open DungeonSaveAndLoad
open DungeonRoomState

/// Applies a DungeonModel to the current dungeon map for the given level (1-indexed)
let applyDungeonModelToLevel (level:int) (dm:DungeonSaveAndLoad.DungeonModel) =
    if level < 1 || level > 9 then
        failwithf "Invalid level: %d. Level must be between 1 and 9." level
    else
        // Use the existing import function for this level
        DungeonUI.importFunctions.[level-1](dm)



// Full function to apply an entire array of DungeonModels
let applyAllDungeonModels (incomingModels: DungeonModel[]) =
    if TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption.IsSome then
        for level = 1 to 9 do
            let incoming = incomingModels.[level-1]  // incomingModels is 0-indexed; applyDungeonModelToLevel is 1-indexed
            applyDungeonModelToLevel level incoming
            // If the incoming model has any marked rooms, mark this dungeon as seen so the summary tab shows content
            if incoming.RoomStates <> null && incoming.RoomStates |> Array.exists (fun row -> row <> null && row |> Array.exists (fun r -> r <> null && not r.IsDefault)) then
                DungeonUI.isFirstTimeClickingAnyRoom.[level-1].Value <- false
    else
        printfn "[Sync] Skipping dungeon sync: TrackerModel not yet initialized."

/// Applies a single CDungeonRoomState update from a JSON payload.
/// The payload must include DungeonIndex (0-8), X, Y, and the room object.
//let applySingleRoomStateFromJson (payloadJson: string) =
//    let jo = JObject.Parse(payloadJson)
//    let dungeonIndex = jo.["DungeonIndex"].ToObject<int>()
//    let x = jo.["X"].ToObject<int>()
//    let y = jo.["Y"].ToObject<int>()
//    let roomModelJson = jo.["Room"].ToString()

//    if TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption.IsSome then
//        let exportFn = DungeonUI.exportFunctions.[dungeonIndex]
//        let importFn = DungeonUI.importFunctions.[dungeonIndex]
//        let currentModel = exportFn()

//        let incomingRoom = JsonConvert.DeserializeObject<DungeonRoomModel>(roomModelJson)

//        // Apply if different
//        let mutable changed = false
//        if shouldUpdateRoom currentModel.RoomStates.[x, y] incomingRoom then
//            applyRoomModel currentModel.RoomStates.[x, y] incomingRoom
//            changed <- true

//        if changed then
//            CoopSync.dungeonMapsSyncOrigin.MarkSyncStart()
//            try
//                importFn currentModel
//                TrackerModel.dungeonRoomModelChanged.SetNow()
//            finally
//                CoopSync.dungeonMapsSyncOrigin.MarkSyncEnd()
