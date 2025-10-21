using System.Diagnostics;
using SchoolBookPlatform.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Controllers
{
    [Authorize]
   public class AuthenController : Controller
    {
        private readonly ILogger<AuthenController> _logger;

        public AuthenController(ILogger<AuthenController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            if(User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Home", "Feeds");
            }
            return View(new LoginViewModel());
        }
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                Console.Write("Login Model invalid: " + string.Join(",", errors));
                _logger.LogError("Login Model invalid: " + string.Join(",", errors));
                return View(model);
            }
            return RedirectToAction("Home", "Feeds");
            //else
            //{

            //}
        }
    }
}