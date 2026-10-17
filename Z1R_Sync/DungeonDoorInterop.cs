using System;
using Z1R_Tracker.Models;
using Z1R_Tracker.Models.Z1R_TrackerInterop;

namespace Z1R_Sync
{
    public static class DungeonDoorInterop
    {
        public static DoorState GetDoorState(int level, int x, int y, DoorDirection direction)
        {
            return CDungeonModelStore.GetDoorState(level, x, y, direction);
        }

        public static void SetDoorState(int level, int x, int y, DoorDirection direction, DoorState state)
        {
            CDungeonModelStore.SetDoorState(level, x, y, direction, state);
        }
    }
}
