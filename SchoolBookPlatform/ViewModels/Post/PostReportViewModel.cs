using System;

namespace SchoolBookPlatform.ViewModels
{
    public class PostReportViewModel
    {
        public Guid Id { get; set; }
        public Guid ReportedBy { get; set; }
        public string ReportedByName { get; set; }
        public string Reason { get; set; }
        public string Status { get; set; }
        public Guid? ReviewedBy { get; set; }
        public string ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}