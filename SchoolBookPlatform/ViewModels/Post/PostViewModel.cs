using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SchoolBookPlatform.ViewModels
{
    public class PostViewModel
    {
        public Guid Id { get; set; }
        
        [Required]
        public Guid UserId { get; set; }
        
        [Required]
        [MaxLength(300)]
        public string Title { get; set; }
        
        public string Content { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? UpdatedAt { get; set; }
        
        public bool IsDeleted { get; set; }
        
        public bool IsVisible { get; set; }
        
        [MaxLength(50)]
        public string? VisibleToRoles { get; set; }
        
        // User information
        public string UserName { get; set; }
        public string? UserEmail { get; set; }
        
        // Statistics
        public int UpvoteCount { get; set; }
        public int DownvoteCount { get; set; }
        public int CommentCount { get; set; }
        public int AttachmentCount { get; set; }
        public int ReportCount { get; set; }
        
        // Current user's interaction
        public bool? CurrentUserVote { get; set; } // null: no vote, true: upvote, false: downvote
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public bool CanReport { get; set; }
        
        // Collections
        public List<PostAttachmentViewModel> Attachments { get; set; } = new List<PostAttachmentViewModel>();
        public List<PostCommentViewModel> Comments { get; set; } = new List<PostCommentViewModel>();
        public List<PostVoteViewModel> RecentVotes { get; set; } = new List<PostVoteViewModel>();
        public List<PostReportViewModel> Reports { get; set; } = new List<PostReportViewModel>();
    }

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

    public class PostCommentViewModel
    {
        public Guid Id { get; set; }
        public Guid PostId { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? ParentCommentId { get; set; }
        public string ParentCommentUserName { get; set; }
        public int ReplyCount { get; set; }
        public List<PostCommentViewModel> Replies { get; set; } = new List<PostCommentViewModel>();
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
    }

    public class PostVoteViewModel
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public bool VoteType { get; set; }
        public DateTime VotedAt { get; set; }
        public string VoteTypeDisplay => VoteType ? "Upvote" : "Downvote";
    }

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