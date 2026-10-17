namespace Z1RSignalRHost.Models
{
    public class SyncPayload
    {
        public int Level { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsComplete { get; set; }
        public string RoomType { get; set; }
        public string MonsterDetail { get; set; }
        public string FloorDropDetail { get; set; }
        public bool FloorDropAppearsBright { get; set; }
    }
}
