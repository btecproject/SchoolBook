using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services;

public class TokenService
{
    private readonly AppDbContext _db;
    public TokenService(AppDbContext db)
    {
        _db = db;
    }

    public async Task SignInAsync(HttpContext ctx, User user)
    {
        //create token
        var token = new UserToken
        {
            UserId = user.Id,
            DeviceInfo = ctx.Request.Headers["User-Agent"].ToString(),
            IPAddress = ctx.Connection.RemoteIpAddress?.ToString(),
            ExpiredAt = DateTime.UtcNow.AddDays(7)
        };
        _db.UserTokens.Add(token);
        await _db.SaveChangesAsync();
        
        //claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, await GetPrimaryRole(user.Id)),
            new Claim("TokenVersion", user.TokenVersion.ToString()),
            new Claim("TokenId", token.Id.ToString())
        };
        
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var props = new AuthenticationProperties
        {
            IsPersistent = true,
            AllowRefresh = true,
            ExpiresUtc = DateTime.UtcNow.AddDays(7)
        };
    }
    private async Task<string> GetPrimaryRole(Guid userId)
    {
        var ur = await _db.UserRoles.Include(ur => ur.Role).FirstOrDefaultAsync(x => x.UserId == userId);
        return ur?.Role?.Name ?? "Student";
    }
    
    //validate invoke
      public static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            try
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var tokenIdClaim = context.Principal?.FindFirst("TokenId")?.Value;
                var tokenVersionClaim = context.Principal?.FindFirst("TokenVersion")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || string.IsNullOrEmpty(tokenIdClaim) || string.IsNullOrEmpty(tokenVersionClaim))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var userId = Guid.Parse(userIdClaim);
                var tokenId = Guid.Parse(tokenIdClaim);

                var user = await db.Users.FindAsync(userId);
                if (user == null || user.TokenVersion.ToString() != tokenVersionClaim)
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                var token = await db.UserTokens.FirstOrDefaultAsync(t => t.TokenId == tokenId && t.UserId == userId);
                if (token == null || token.IsRevoked || (token.ExpiredAt.HasValue && token.ExpiredAt < DateTime.UtcNow))
                {
                    context.RejectPrincipal();
                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    return;
                }

                // if valid -> extend server-side expiry (sliding)
                token.ExpiredAt = DateTime.UtcNow.AddDays(7);
                await db.SaveChangesAsync();
            }
            catch
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }

        // Revoke methods
        public async Task RevokeAllAsync(Guid userId)
        {
            var tokens = _db.UserTokens.Where(t => t.UserId == userId && !t.IsRevoked);
            foreach (var t in tokens) t.IsRevoked = true;
            var user = await _db.Users.FindAsync(userId);
            if (user != null) user.TokenVersion++;
            await _db.SaveChangesAsync();
        }

        public async Task RevokeTokenAsync(Guid tokenId)
        {
            var token = await _db.UserTokens.FirstOrDefaultAsync(t => t.TokenId == tokenId);
            if (token != null)
            {
                token.IsRevoked = true;
                await _db.SaveChangesAsync();
            }
        }

        public async Task RevokeCurrentAsync(ClaimsPrincipal principal)
        {
            var tid = principal.FindFirst("TokenId")?.Value;
            if (Guid.TryParse(tid, out var tokenId))
                await RevokeTokenAsync(tokenId);
        }
}