using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

public class TokenService
{
    private readonly AppDbContext _db;

    public TokenService(AppDbContext db)
    {
        _db = db;
    }

    public async Task SignInAsync(HttpContext ctx, User user)
    {
        var token = new UserToken
        {
            UserId = user.Id,
            ExpiredAt = DateTime.UtcNow.AddDays(7)
        };

        _db.UserTokens.Add(token);
        await _db.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("TokenId", token.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTime.UtcNow.AddDays(7)
        });
    }

    public async Task SignOutAsync(HttpContext ctx)
    {
        var tokenId = ctx.User.FindFirst("TokenId")?.Value;
        if (Guid.TryParse(tokenId, out var tid))
        {
            var token = await _db.UserTokens.FindAsync(tid);
            if (token != null)
            {
                token.IsRevoked = true;
                await _db.SaveChangesAsync();
            }
        }

        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}