using System;

namespace SchoolBookPlatform.ViewModels
{
    public class PostAttachmentViewModel
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public int FileSize { get; set; }
        public string FileSizeFormatted { get; set; }
        public DateTime UploadedAt { get; set; }
        public string FileType { get; set; }
    }
}