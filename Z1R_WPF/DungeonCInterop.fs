module DungeonCInterop

open Z1R_Tracker.Models
open Z1R_Tracker.Models.Z1R_TrackerInterop
open TrackerModelOptions

/// Get a specific room state for a dungeon level
let getRoomState (level: int) (x: int) (y: int) : CDungeonRoomState =
    CDungeonModelStore.Dungeons.[level].[x, y]

/// Get the entire 8x8 grid for a dungeon level
let getDungeonGrid (level: int) : CDungeonRoomState[,] =
    CDungeonModelStore.Dungeons.[level]

let createRoom () = CDungeonRoomState()

let getRoomType (room: CDungeonRoomState) = room.RoomType
let setRoomType (room: CDungeonRoomState) (rt: RoomType) = room.RoomType <- rt

let getIsComplete (room: CDungeonRoomState) = room.IsComplete
let setIsComplete (room: CDungeonRoomState) (v: bool) = room.IsComplete <- v

let subscribeToChanges (room: CDungeonRoomState) (handler: unit -> unit) =
    room.Changed.Add(fun _ -> handler())
