namespace SchoolBookPlatform.ViewModels.Profile;

public class UserPostViewModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? PreviewContent { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
    public int CommentCount { get; set; }
    public int AttachmentCount { get; set; }
    public bool HasImages { get; set; }
    public List<string>? ImageUrls { get; set; } // URL của ảnh (nếu có)
}