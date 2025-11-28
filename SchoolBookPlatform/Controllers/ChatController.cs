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
    public async Task<IActionResult> Index()
    {
        var currentUser = await HttpContext.GetCurrentUserAsync(db);
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Authen");
        }
        if (await chatService.IsRegisterChatService(currentUser.Id) == false)
        {
            return RedirectToAction("RegisterChatUser");
        }

        if (TempData["PinCodeMatched"] != null && TempData["PinCodeMatched"]!.Equals("true"))
        {
            return View();
        }
        return RedirectToAction("PinCodeAuthen");
    }

    [HttpPost]
    public IActionResult RegisterChatUser()
    {
        throw new NotImplementedException();
    }

    public IActionResult PinCodeAuthen()
    {
        throw new NotImplementedException();
    }
}