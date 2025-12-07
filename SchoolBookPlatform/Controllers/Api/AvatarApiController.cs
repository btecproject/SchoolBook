using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform.Controllers.Api;
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class AvatarApiController(AvatarService avatarService,
    AppDbContext db,
    ILogger<AvatarApiController> logger) : ControllerBase
{
    public async Task<ActionResult> Get([FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("userId require");
        var parseUserId = Guid.TryParse(userId, out var id);
        if (!parseUserId)
        {
            logger.LogError("AvatarApi: userId is invalid");
            return BadRequest("userId is invalid");
        }

        var user = await db.GetUserByIdAsync(id);

        if (user == null)
            return NotFound("Cannot find user");

        var avatarUrl = avatarService.GetAvatar(user);
        
        return Redirect(avatarUrl);
    }
}