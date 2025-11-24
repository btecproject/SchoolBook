using System;

namespace SchoolBookPlatform.ViewModels
{
    public class PostVoteViewModel
    {
        public Guid UserId { get; set; }
        public string UserName { get; set; }
        public bool VoteType { get; set; }
        public DateTime VotedAt { get; set; }
        public string VoteTypeDisplay => VoteType ? "Upvote" : "Downvote";
    }
}