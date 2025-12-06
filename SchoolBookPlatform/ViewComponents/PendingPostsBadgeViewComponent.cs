using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;

namespace SchoolBookPlatform.ViewComponents;

/// <summary>
/// ViewComponent hiển thị badge số lượng bài đăng chờ duyệt
/// </summary>
public class PendingPostsBadgeViewComponent : ViewComponent
{
    private readonly AppDbContext _db;

    public PendingPostsBadgeViewComponent(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var pendingCount = await _db.Posts
            .CountAsync(p => !p.IsDeleted && !p.IsVisible);

        return View(pendingCount);
    }
}

