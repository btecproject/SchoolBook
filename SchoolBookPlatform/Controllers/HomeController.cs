using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Controllers;

public class HomeController(ILogger<HomeController> logger,
    AppDbContext _context,
    AvatarService avatarService) : Controller
{
    private readonly ILogger<HomeController> _logger = logger;
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Home", "Feeds");
        }
        return View();
    }
    [HttpGet]
    public async Task<IActionResult> SearchUsers(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Json(new { success = true, data = new List<object>() });
        }
        
        var users = await _context.Users
            .Where(u => u.IsActive && 
                        (u.Username.Contains(query) || u.Email!.Contains(query)))
            .Take(5)
            .ToListAsync();

        var result = users.Select(u => new 
        {
            id = u.Id,
            username = u.Username,
            avatar = avatarService.GetAvatar(u),
        });

        return Json(new { success = true, data = result });
    }
    
    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Term()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}