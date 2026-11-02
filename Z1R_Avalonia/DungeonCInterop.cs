// Ported from Z1R_WPF/DungeonCInterop.fs (WPF @ 9292b0b). Same members, same order; see docs/avalonia-port/PLAN.md, "Language: C#".
// (F#'s `open TrackerModelOptions` is unused here. Curried functions take normal parameters.)
using System;
using Z1R_Tracker.Models;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Avalonia;

public static class DungeonCInterop
{
    /// Get a specific room state for a dungeon level
    public static CDungeonRoomState getRoomState(int level, int x, int y) =>
        CDungeonModelStore.Dungeons[level][x, y];

    /// Get the entire 8x8 grid for a dungeon level
    public static CDungeonRoomState[,] getDungeonGrid(int level) =>
        CDungeonModelStore.Dungeons[level];

    public static CDungeonRoomState createRoom() => new CDungeonRoomState();

    public static RoomType getRoomType(CDungeonRoomState room) => room.RoomType;
    public static void setRoomType(CDungeonRoomState room, RoomType rt) => room.RoomType = rt;

    public static bool getIsComplete(CDungeonRoomState room) => room.IsComplete;
    public static void setIsComplete(CDungeonRoomState room, bool v) => room.IsComplete = v;

    public static void subscribeToChanges(CDungeonRoomState room, Action handler) =>
        room.Changed += (_, _) => handler();
}
