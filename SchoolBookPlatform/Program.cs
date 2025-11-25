using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Http.Features;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Hubs;
using SchoolBookPlatform.Services;

namespace SchoolBookPlatform;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
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
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = TokenService.ValidateAsync
                };
            }).AddGoogle(options =>
            {
                options.ClientId = google["ClientId"];
                options.ClientSecret = google["ClientSecret"];
                options.CallbackPath = "/signin-google";
            });
        
        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 100_000_000; // 100MB
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = 100_000_000; // 100MB
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
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        
        // Route cho Chat MVC Views
        app.MapControllerRoute(
            name: "chat",
            pattern: "Chat/{action=Index}/{threadId?}",
            defaults: new { controller = "Chat" });
        
        // Route cho TokenManager
        app.MapControllerRoute(
            "tokenmanager",
            "TokenManager/{action=Index}/{id?}",
            new { controller = "TokenManager" });
        


        app.Run();
    }
}