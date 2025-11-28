using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Controllers;

public class ChatController(
    AppDbContext db,
    ChatService chatService,
    ILogger<ChatController> logger) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    // [HttpGet]
    // public async Task<IActionResult> Index(string username)
    // {
    //     var targetUser = await db.GetUserWithProfileAsync(username);
    //     if (targetUser == null) return NotFound();
    //     var currentUser = await HttpContext.GetCurrentUserAsync(db);
    //     if (currentUser == null) return Unauthorized();
    //     var isOwner = currentUser.Id = targetUser!.Id;
    // }
}