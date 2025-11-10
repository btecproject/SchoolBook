using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var config = builder.Configuration;

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

        // Authentication
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Authen/Login";
                options.LogoutPath = "/Authen/Logout";
                options.AccessDeniedPath = "/Authen/AccessDenied";
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = TokenService.ValidateAsync
                };
            });

        // Logging
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        // Authorization + Policy
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("HighAdminOnly", policy =>
                policy.RequireRole("HighAdmin"));
            
            options.AddPolicy("AdminOrHigher", policy =>
                policy.RequireRole("HighAdmin", "Admin"));
        });

        builder.Services.AddControllersWithViews();

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

        // Route mặc định: Home/Index → Chào mừng
        app.MapControllerRoute(
            "default",
            "{controller=Home}/{action=Index}");

        // Route cho TokenManager
        app.MapControllerRoute(
            "tokenmanager",
            "TokenManager/{action=Index}/{id?}",
            new { controller = "TokenManager" });

        app.Run();
    }
}