namespace SchoolBookPlatform.Models
{
    public class ChatAttachment
    {
        public int Id { get; set; }
        public int SegmentId { get; set; }
        public int MessageIndex { get; set; } 
        public string FileName { get; set; }
        public string FileType { get; set; } 
        public string MimeType { get; set; }
        public long FileSize { get; set; }
        public byte[] FileData { get; set; }
        public DateTime UploadedAt { get; set; }
        
    }
}