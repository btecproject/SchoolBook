using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services;

public class TokenService(AppDbContext db, ILogger<TokenService> logger)
{
    public async Task SignInAsync(HttpContext ctx, User user, AppDbContext db)
    {
        var token = new UserToken
        {
            UserId = user.Id,
            ExpiredAt = DateTime.UtcNow.AddDays(7)
        };

        db.UserTokens.Add(token);
        await db.SaveChangesAsync();

        IEnumerable<string> roleNames = await db.GetUserRolesAsync(user.Id);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("TokenId", token.Id.ToString())
        };
        foreach (var roleName in roleNames)
        {
            claims.Add(new Claim(ClaimTypes.Role, roleName));
        }

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
            var token = await db.UserTokens.FindAsync(tid);
            if (token != null)
            {
                token.IsRevoked = true;
                await db.SaveChangesAsync();
            }
        }

        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public static bool IsExternalLoginMethodAsync(CookieValidatePrincipalContext context)
    {
        var authMethod = context.Principal?.FindFirst("AuthenticationMethod")?.Value;
        if (authMethod == "Google" || context.Principal?.Claims.Any(c => c.Issuer.Contains("Google")) == true)
        {
            return true;
        }
        return false;
    }
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var tokenIdClaim = context.Principal?.FindFirst("TokenId")?.Value;
        var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(tokenIdClaim) || string.IsNullOrEmpty(userIdClaim))
        {
            if (IsExternalLoginMethodAsync(context))
            {
                return;
            }
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (!Guid.TryParse(tokenIdClaim, out var tokenId) || !Guid.TryParse(userIdClaim, out var userId))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        // Lấy DbContext từ DI
        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var logger = context.HttpContext.RequestServices.GetService<ILogger<TokenService>>();

        // Kiểm tra token trong database
        var token = await db.UserTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId);

        if (token == null || token.IsRevoked || token.ExpiredAt < DateTime.UtcNow)
        {
            logger?.LogWarning("Invalid token {TokenId} for user {UserId}. Revoked: {IsRevoked}, Expired: {ExpiredAt}",
                tokenId, userId, token?.IsRevoked, token?.ExpiredAt);

            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        
        context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7);
        context.ShouldRenew = true;
        // Token hợp lệ
        logger?.LogDebug("Token {TokenId} validated successfully for user {UserId}", tokenId, userId);
        TokenService ts =  new TokenService(db, logger);
    }
    public async Task<bool> RevokeAllTokensAsync(Guid userId)
    {
        try
        {
            var user = await db.Users.FindAsync(userId);
            if (user == null)
                return false;

            // Tăng TokenVersion để invalidate tất cả tokens hiện tại
            user.TokenVersion++;
            user.UpdatedAt = DateTime.UtcNow;

            // Revoke tất cả tokens trong database
            var tokens = await db.UserTokens
                .Where(t => t.UserId == userId && !t.IsRevoked)
                .ToListAsync();

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
            }

            await db.SaveChangesAsync();
            logger.LogInformation("All tokens revoked for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error revoking tokens for user {UserId}", userId);
            return false;
        }
    }
}