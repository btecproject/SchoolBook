namespace SchoolBookPlatform.ViewModels.Post
{
    public class PostListViewModel
    {
        public List<PostViewModel> Posts { get; set; } = new();
        public bool IsEmptyFollowing { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; }
        public int TotalPosts { get; set; }
        public int FollowingCount { get; set; }
        public int FollowerCount { get; set; }
        public string ViewType { get; set; } = "home"; // "index", "following", "home"
        public int TotalItems { get; set; }
        public bool HasMorePosts { get; set; } = true;
        
        public string SortBy { get; set; } = "newest";
        public string FilterRole { get; set; } = "All";
        public List<string> AvailableRoles { get; set; } = new List<string> { "All" };
        
        // Helper properties
        public bool IsNewestSort => SortBy == "newest";
        public bool IsHotSort => SortBy == "hot";
        public bool IsBestSort => SortBy == "best";
    }
}