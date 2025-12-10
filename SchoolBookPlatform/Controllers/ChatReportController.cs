using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class ChatReportController(
    AppDbContext db,
    ChatService chatService) : Controller
{
    [HttpGet]
    [Authorize(Policy = "ModeratorOrHigher")]
    public IActionResult Index()
    {
        return View();
    }
}