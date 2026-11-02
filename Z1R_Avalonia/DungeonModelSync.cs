// Ported from Z1R_WPF/DungeonModelSync.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// (F#'s `open SaveAndLoad` / `open DungeonRoomState` / `open Newtonsoft.Json` / `open Newtonsoft.Json.Linq` are only used by the commented-out code at the end;
// `open DungeonSaveAndLoad` is a `using` alias for DungeonRoomModel/DungeonModel. Curried functions take normal parameters.)
using System;
using System.Linq;
using DungeonRoomModel = Z1R_Avalonia.DungeonSaveAndLoad.DungeonRoomModel;
using DungeonModel = Z1R_Avalonia.DungeonSaveAndLoad.DungeonModel;

namespace Z1R_Avalonia;

public static class DungeonModelSync
{
    // Helper: Compare two DungeonRoomModels
    public static bool shouldUpdateRoom(DungeonRoomModel current, DungeonRoomModel incoming) =>
        current.IsCompleted != incoming.IsCompleted ||
        current.RoomType != incoming.RoomType ||
        current.MonsterDetail != incoming.MonsterDetail ||
        current.FloorDropDetail != incoming.FloorDropDetail ||
        current.FloorDropShouldAppearBright != incoming.FloorDropShouldAppearBright;

    // Helper: Apply properties from one DungeonRoomModel to another
    public static void applyRoomModel(DungeonRoomModel current, DungeonRoomModel incoming)
    {
        current.IsCompleted = incoming.IsCompleted;
        current.RoomType = incoming.RoomType;
        current.MonsterDetail = incoming.MonsterDetail;
        current.FloorDropDetail = incoming.FloorDropDetail;
        current.FloorDropShouldAppearBright = incoming.FloorDropShouldAppearBright;
    }

    // Main function to apply a single DungeonModel update
    // (F#: open DungeonSaveAndLoad / open DungeonRoomState, again)

    /// Applies a DungeonModel to the current dungeon map for the given level (1-indexed)
    public static void applyDungeonModelToLevel(int level, DungeonSaveAndLoad.DungeonModel dm)
    {
        if (level < 1 || level > 9)
            throw new Exception($"Invalid level: {level}. Level must be between 1 and 9.");   // (F#: failwithf)
        else
        {
            // Use the existing import function for this level
            DungeonUI.importFunctions[level - 1](dm);
        }
    }



    // Full function to apply an entire array of DungeonModels
    public static void applyAllDungeonModels(DungeonModel[] incomingModels)
    {
        if (Microsoft.FSharp.Core.FSharpOption<TrackerModel.DungeonTrackerInstance>.get_IsSome(TrackerModel.DungeonTrackerInstance.TheDungeonTrackerInstanceOption))
        {
            for (int level = 1; level <= 9; level++)
            {
                var incoming = incomingModels[level - 1];  // incomingModels is 0-indexed; applyDungeonModelToLevel is 1-indexed
                applyDungeonModelToLevel(level, incoming);
                // If the incoming model has any marked rooms, mark this dungeon as seen so the summary tab shows content
                if (incoming.RoomStates != null && incoming.RoomStates.Any(row => row != null && row.Any(r => r != null && !r.IsDefault)))
                    DungeonUI.isFirstTimeClickingAnyRoom[level - 1].Value = false;
            }
        }
        else
            Console.WriteLine("[Sync] Skipping dungeon sync: TrackerModel not yet initialized.");
    }

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
}
