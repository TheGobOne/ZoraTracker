namespace Z1R_SignalRHost.Models
{
    public class SyncUpdatePayload
    {
        public string MessageType { get; set; }
        public string SenderId { get; set; }
        public object Payload { get; set; }

        public long TimeStamp { get; set; }
    }
}
