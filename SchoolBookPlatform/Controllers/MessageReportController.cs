using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.DTOs;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels.MessageReport;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class MessageReportController(AppDbContext db, MessageReportService reportService): Controller
{
    //cho tat ca
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateMessageReportRequest request)
    {
        var user = await HttpContext.GetCurrentUserAsync(db);
        if (user == null) return Unauthorized();

        var result = await reportService.CreateReportAsync(user.Id, request);
        if (!result.Success) return BadRequest(new { message = result.Message });

        return Ok(new { message = "Report created successfully, thank you for reporting" });
    }
    
    //xem ds
    [Authorize(Policy = "ModeratorOrHigher")]
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        int pageSize = 20; 
        if (page < 1) page = 1;

        var data = await reportService.GetPendingReportsAsync(page, pageSize);

        var viewModel = new PaginatedReportViewModel
        {
            Reports = data.Reports,
            CurrentPage = page,
            TotalPages = (int)Math.Ceiling(data.TotalCount / (double)pageSize)
        };

        return View(viewModel);
    }
    
    [Authorize(Policy = "ModeratorOrHigher")]
    [HttpPost]
    public async Task<IActionResult> Resolve([FromBody] ResolveReportRequest request)
    {
        var user = await HttpContext.GetCurrentUserAsync(db);
        if (user == null) return Unauthorized();

        var result = await reportService.ResolveReportAsync(user.Id, request);
        if (!result.Success) return BadRequest(new { message = result.Message });

        return Ok(new { message = "Report resolved successfully" });
    }
    
}