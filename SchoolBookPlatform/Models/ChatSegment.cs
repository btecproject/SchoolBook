namespace SchoolBookPlatform.Models
{
    public class ChatSegment
    {
        public int Id { get; set; }
        public int ThreadId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public string MessagesJson { get; set; } = "[]";
        public bool IsProtected { get; set; }
        public string PinHash { get; set; } 
        public byte[] Salt { get; set; }  
    }
}