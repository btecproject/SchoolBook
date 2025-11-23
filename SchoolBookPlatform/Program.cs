using CloudinaryDotNet;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // builder.WebHost.UseUrls("https://10.24.19.178:7093");
        var config = builder.Configuration;
        var google = config.GetSection("Authentication:Google");
        // DB
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")));
        // Services
        builder.Services.AddHttpClient();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<TokenService>();
        builder.Services.AddScoped<FaceService>();
        builder.Services.AddScoped<OtpService>();
        builder.Services.AddScoped<TrustedService>();
        builder.Services.AddScoped<UserManagementService>();
        builder.Services.AddScoped<GoogleAuthenService>();
        builder.Services.AddScoped<TwoFactorService>();
        builder.Services.AddScoped<AvatarService>();
        builder.Services.AddScoped<RecoveryCodeService>();
        builder.Services.AddSingleton<Cloudinary>(sp =>
        {
            var config = builder.Configuration.GetSection("Cloudinary");
            var account = new CloudinaryDotNet.Account(
                config["CloudName"],
                config["ApiKey"],
                config["ApiSecret"]
            );
            return new Cloudinary(account);
        });
        
        // Logging
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        
        builder.Services.AddControllersWithViews();
        
        // Authentication
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/Authen/Login";
                options.LogoutPath = "/Authen/Logout";
                options.AccessDeniedPath = "/Authen/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = TokenService.ValidateAsync
                };
            }).AddGoogle(GoogleDefaults.AuthenticationScheme,options =>
            {
                options.ClientId = google["ClientId"]!;
                options.ClientSecret = google["ClientSecret"]!;
                options.CallbackPath = "/signin-google";
                options.SaveTokens = true;
            });
        
        // Authorization + Policy
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("HighAdminOnly", policy =>
                policy.RequireRole("HighAdmin"));
            
            options.AddPolicy("AdminOrHigher", policy =>
                policy.RequireRole("HighAdmin", "Admin"));
        });
        
        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapStaticAssets();

        // MINIMAL API
        app.MapPost("/api/posts/{id}/delete", async (Guid id, HttpContext context, AppDbContext dbContext) =>
        {
            try
            {
                // Kiểm tra authentication
                if (!context.User.Identity?.IsAuthenticated ?? false)
                {
                    return Results.Json(new { success = false, message = "Chưa đăng nhập" }, statusCode: 401);
                }

                var post = await dbContext.Posts
                    .Include(p => p.Attachments)
                    .Include(p => p.Comments)
                    .Include(p => p.Votes)
                    .Include(p => p.Reports)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (post == null)
                {
                    return Results.Json(new { success = false, message = "Bài viết không tồn tại." });
                }

                // Kiểm tra quyền xóa
                var currentUserId = GetCurrentUserId(context.User);
                var isAdmin = context.User.IsInRole("Admin") || context.User.IsInRole("HighAdmin");
                
                if (post.UserId != currentUserId && !isAdmin)
                {
                    return Results.Json(new { success = false, message = "Bạn không có quyền xóa bài viết này." });
                }

                // Xóa tất cả dữ liệu liên quan
                if (post.Attachments?.Any() == true)
                    dbContext.PostAttachments.RemoveRange(post.Attachments);

                if (post.Comments?.Any() == true)
                    dbContext.PostComments.RemoveRange(post.Comments);

                if (post.Votes?.Any() == true)
                    dbContext.PostVotes.RemoveRange(post.Votes);

                if (post.Reports?.Any() == true)
                    dbContext.PostReports.RemoveRange(post.Reports);

                // Xóa bài đăng chính
                dbContext.Posts.Remove(post);
                await dbContext.SaveChangesAsync();

                return Results.Json(new { success = true, message = "Bài viết đã được xóa thành công." });
            }
            catch (Exception ex)
            {
                // Log lỗi
                app.Logger.LogError(ex, "Lỗi khi xóa bài viết {PostId}", id);
                return Results.Json(new { success = false, message = "Có lỗi xảy ra khi xóa bài viết." });
            }
        }).RequireAuthorization(); // Yêu cầu đăng nhập

        // Route mặc định: Feeds/Home → Trang chủ feed
        app.MapControllerRoute(
            "default",
            "{controller=Feeds}/{action=Home}/{id?}");

        app.MapGet("/debug/routes", (IEnumerable<EndpointDataSource> endpointSources) =>
        {
            var endpoints = endpointSources.SelectMany(es => es.Endpoints);
            return Results.Json(endpoints.Select(e =>
            {
                var route = (e as RouteEndpoint)?.RoutePattern.RawText ?? "N/A";
                var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? new List<string>();
                return new
                {
                    Route = route,
                    Methods = string.Join(", ", methods),
                    DisplayName = e.DisplayName
                };
            }));
        });

        app.Run();
    }

    // Helper method để lấy UserId
    private static Guid GetCurrentUserId(System.Security.Claims.ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(userIdClaim, out Guid userId))
        {
            return userId;
        }
        
        userIdClaim = user.FindFirst("UserId")?.Value;
        if (Guid.TryParse(userIdClaim, out userId))
        {
            return userId;
        }
        
        return Guid.Empty;
    }
}