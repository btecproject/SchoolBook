using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

public class Post
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(300)]
    public string? Title { get; set; }

    public string? Content { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    public bool IsVisible { get; set; } = true;

    [MaxLength(50)]
    public string? VisibleToRoles { get; set; } // e.g., "Student", "Teacher", "Admin", "All"

    [ForeignKey("UserId")]
    public virtual User User { get; set; }

    public virtual ICollection<PostComment> Comments { get; set; } = new List<PostComment>();

    public virtual ICollection<PostAttachment> Attachments { get; set; } = new List<PostAttachment>();

    public virtual ICollection<PostVote> Votes { get; set; } = new List<PostVote>();

    public virtual ICollection<PostReport> Reports { get; set; } = new List<PostReport>();
}