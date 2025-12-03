using CloudinaryDotNet;
using Ganss.Xss;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Filters;
using SchoolBookPlatform.Hubs;
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
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<HtmlSanitizer>(new HtmlSanitizer());
        builder.Services.AddScoped<TokenService>();
        builder.Services.AddScoped<FaceService>();
        builder.Services.AddScoped<OtpService>();
        builder.Services.AddScoped<TrustedService>();
        builder.Services.AddScoped<UserManagementService>();
        builder.Services.AddScoped<GoogleAuthenService>();
        builder.Services.AddScoped<TwoFactorService>();
        builder.Services.AddScoped<AvatarService>();
        builder.Services.AddScoped<RecoveryCodeService>();
        builder.Services.AddScoped<CloudinaryService>();
        builder.Services.AddScoped<ChatService>();
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
        
        builder.Services.AddControllersWithViews(options =>
        {
            //Dky xss filter 
            options.Filters.Add<AntiXssFilter>();
        });        
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
                options.Cookie.HttpOnly = true; //js ko đụng cookies
                options.Cookie.SameSite = SameSiteMode.Lax; // chỉ get hợp lệ từ link
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always; //chỉ https

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
        app.Use(async (context, next) =>
        {
            // Cấu hình CSP chặt chẽ
            context.Response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " + 
                // Chỉ cho phép script từ domain mình và các CDN tin cậy
                "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://code.jquery.com; " +
                // Chỉ cho phép style từ domain mình và CDN
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com; " +
                // Cho phép ảnh từ Cloudinary, Google
                "img-src 'self' data: https://res.cloudinary.com https://*.googleusercontent.com; " +
                // Font chữ
                "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net; " +
                // Cho phép kết nối WebSocket (SignalR)
                "connect-src 'self' wss: https:; " +
                // Chặn nhúng web vào iframe (Chống Clickjacking)
                "frame-ancestors 'self';");

            // Các header bảo mật khác nên có
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

            await next();
        });
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        
        app.MapStaticAssets();

        // Route mặc định: Home/Index → Chào mừng
        app.MapControllerRoute(
            "default",
            "{controller=Home}/{action=Index}");
        
        app.MapHub<ImportExcelHub>("/importExcelHub");
        app.MapHub<ChatHub>("/chatHub");
        
        // Route cho TokenManager
        // app.MapControllerRoute(
        //     "tokenmanager",
        //     "TokenManager/{action=Index}/{id?}",
        //     new { controller = "TokenManager" });

        app.Run();
    }
}