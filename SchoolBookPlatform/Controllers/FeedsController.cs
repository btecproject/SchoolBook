using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class FeedsController(
    TrustedService trustedService) : Controller
{
    public IActionResult Home()
    {
        return View();
    }
    /// <summary>
    /// Test Lấy IP
    /// </summary>
    public IActionResult GetIp()
    {
        var info = trustedService.GetDeviceInfoAsync(HttpContext);
        var ip = trustedService.GetDeviceIpAsync(HttpContext);
        TempData["Ip + Info"] = ip + " | " + info;
        return View(nameof(Home));
    }
}