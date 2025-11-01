using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class FeedsController : Controller
{
    public IActionResult Home()
    {
        return View();
    }
}