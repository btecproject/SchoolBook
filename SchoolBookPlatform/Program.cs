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
}