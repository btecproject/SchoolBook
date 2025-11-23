using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

public class PostReport
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid PostId { get; set; }

    [Required]
    public Guid ReportedBy { get; set; }

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Pending"; // "Pending", "Approved", "Rejected"

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("PostId")]
    public virtual Post Post { get; set; }

    [ForeignKey("ReportedBy")]
    public virtual User ReportedByUser { get; set; }

    [ForeignKey("ReviewedBy")]
    public virtual User ReviewedByUser { get; set; }
}