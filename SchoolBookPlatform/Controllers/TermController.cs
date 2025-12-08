using Microsoft.AspNetCore.Mvc;

namespace SchoolBookPlatform.Controllers;

public class TermController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}