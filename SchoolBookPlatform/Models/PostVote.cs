using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolBookPlatform.Models;

public class PostVote
{
    [Required]
    public Guid PostId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public bool VoteType { get; set; } // 0: Downvote, 1: Upvote

    public DateTime VotedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("PostId")]
    public virtual Post Post { get; set; }

    [ForeignKey("UserId")]
    public virtual User User { get; set; }
}