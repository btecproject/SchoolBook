namespace SchoolBookPlatform.ViewModels.Post
{
    public class PostListViewModel
    {
        public List<PostViewModel> Posts { get; set; } = new();
        public bool IsEmptyFollowing { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages { get; set; }
        public int TotalPosts { get; set; }
        public int FollowingCount { get; set; }
        public int FollowerCount { get; set; }
        public string ViewType { get; set; } = "index"; // "index", "following", "home"
        
        // Tính toán số trang
        public int GetTotalPages()
        {
            if (TotalPosts == 0 || PageSize == 0) return 1;
            return (int)Math.Ceiling((double)TotalPosts / PageSize);
        }
    }
}