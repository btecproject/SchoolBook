using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CloudinaryDotNet;
using Microsoft.AspNetCore.Http.Features;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SchoolBookPlatform.Hubs;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        // builder.WebHost.UseUrls("https://10.24.37.235:7093");
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
        builder.Services.AddScoped<ChatService>();
        builder.Services.AddSingleton<EncryptionService>();

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
        
        builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 102400;
        });
        
        builder.Services.AddControllersWithViews();
        
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
                policy.WithOrigins("https://localhost:5001", "http://localhost:5000")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
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
        app.UseCors("AllowAll");
        app.MapStaticAssets();
        app.MapControllers();
        
        // Map SignalR Hub
        app.MapHub<ChatHub>("/chatHub");
        app.MapControllers();

        // Route mặc định: Home/Index → Chào mừng
        app.MapControllerRoute(
            "default",
            "{controller=Home}/{action=Index}");
        
        // Route cho Chat MVC Views
        app.MapControllerRoute(
            name: "chat",
            pattern: "Chat/{action=Index}/{threadId?}",
            defaults: new { controller = "Chat" });

        // Route cho TokenManager
        // app.MapControllerRoute(
        //     "tokenmanager",
        //     "TokenManager/{action=Index}/{id?}",
        //     new { controller = "TokenManager" });

        app.Run();
    }
}