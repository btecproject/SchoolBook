using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;

namespace SchoolBookPlatform.ViewComponents;

/// <summary>
/// ViewComponent hiển thị badge số lượng báo cáo đang chờ xử lý
/// </summary>
public class PendingReportsBadgeViewComponent : ViewComponent
{
    private readonly AppDbContext _db;

    public PendingReportsBadgeViewComponent(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var pendingCount = await _db.PostReports
            .CountAsync(r => r.Status == "Pending");

        return View(pendingCount);
    }
}

