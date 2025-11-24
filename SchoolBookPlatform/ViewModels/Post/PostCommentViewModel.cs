using System;
using System.Collections.Generic;

namespace SchoolBookPlatform.ViewModels
{
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
}