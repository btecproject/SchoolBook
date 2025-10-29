using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class FeedsController : Controller
{
    // GET
    public IActionResult Home()
    {
        return View();
    }
}