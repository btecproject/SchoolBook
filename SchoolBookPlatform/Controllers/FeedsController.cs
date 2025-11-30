using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels;
using System.Security.Claims;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class FeedsController : Controller
{
    private readonly TrustedService _trustedService;
    private readonly AppDbContext _context;

    public FeedsController(TrustedService trustedService, AppDbContext context)
    {
        _trustedService = trustedService;
        _context = context;
    }

    /// <summary>
    /// Test lấy IP
    /// </summary>
    public IActionResult GetIp()
    {
        var info = _trustedService.GetDeviceInfoAsync(HttpContext);
        var ip = _trustedService.GetDeviceIpAsync(HttpContext);

        TempData["Ip + Info"] = ip + " | " + info;

        return RedirectToAction(nameof(Home));
    }
}