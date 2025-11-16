using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Services;
using SchoolBookPlatform.ViewModels;

namespace SchoolBookPlatform.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly AppDbContext _db;
    private readonly UserManagementService _userManagementService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        AppDbContext db,
        UserManagementService userManagementService,
        ILogger<ProfileController> logger)
    {
        _db = db;
        _userManagementService = userManagementService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .Include(u => u.Profile)  
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        var viewModel = new ProfileViewModel
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            CreatedAt = user.CreatedAt,

            // From UserProfile
            AvatarUrl = user.Profile?.AvatarUrl,
            FullName = user.Profile?.FullName,
            Bio = user.Profile?.Bio,
            Gender = user.Profile?.Gender,
            BirthDate = user.Profile?.BirthDate
        };

        ViewData["Title"] = $"Hồ sơ của {user.Username}";
        return View(viewModel);
    }
    
    [HttpPost]
    public async Task<IActionResult> UpdateField([FromBody] UpdateFieldRequest req)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return NotFound();

        switch (req.Field)
        {
            case "FullName":
                user.Profile!.FullName = req.Value;
                break;
            case "Gender":
                user.Profile!.Gender = req.Value;
                break;
            case "Bio":
                user.Profile!.Bio = req.Value;
                break;
            case "PhoneNumber":
                user.PhoneNumber = req.Value;
                break;
            case "BirthDate":
                if (DateTime.TryParse(req.Value, out var date))
                    user.Profile!.BirthDate = date;
                break;
            default:
                return BadRequest("Invalid field");
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    public class UpdateFieldRequest
    {
        public string Field { get; set; } = "";
        public string Value { get; set; } = "";
    }

}
