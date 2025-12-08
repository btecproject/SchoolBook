using System.Threading.RateLimiting;
using CloudinaryDotNet;
using Ganss.Xss;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
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
        
        // Scoped Services
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
        
        // Post feature service
        builder.Services.AddScoped<PostService>();
        
        // Cloudinary
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
        
        // Rate Limiter
        builder.Services.AddRateLimiter(options =>
        {
            options.OnRejected = async (context, token) =>
            {
                var httpContext = context.HttpContext;
    
                // Lấy retryAfter
                var retryAfter = "10";
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry))
                {
                    retryAfter = ((int)retry.TotalSeconds).ToString();
                }
    
                // Redirect đến static file với parameter
                httpContext.Response.Redirect($"/429.html?retryAfter={retryAfter}");
            };
            
            // Login 10/10p (Ip)
            options.AddPolicy("LoginPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0 // Ko xếp hàng, chặn luôn
                    }));
            
            // Otp 5/3p
            options.AddPolicy("OtpPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(3),
                        QueueLimit = 0
                    }));
            
            // Chat PIN : 5/3p Ip()
            options.AddPolicy("ChatPinPolicy", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(3),
                        QueueLimit = 0
                    }));
            
            // Chat text: 20token +10/5s Ip
            options.AddPolicy("ChatPolicy", httpContext =>
                RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 25,         
                        TokensPerPeriod = 15,      
                        ReplenishmentPeriod = TimeSpan.FromSeconds(5), 
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 50,          
                        AutoReplenishment = true   // Tự động bổ sung token
                    }));
            
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                // Bỏ qua Rate Limit cho các file tĩnh hoặc API cụ thể nếu cần
                if (context.Request.Path.StartsWithSegments("/lib") || 
                    context.Request.Path.StartsWithSegments("/css") ||
                    context.Request.Path.StartsWithSegments("/js") ||
                    context.Request.Path.StartsWithSegments("/Admin"))
                {
                    return RateLimitPartition.GetNoLimiter("StaticFiles");
                }

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown", 
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = 200, 
                        Window = TimeSpan.FromMinutes(1)
                    });
            });
        });
        
        // Logging
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        
        // Controllers with Anti-XSS filter
        builder.Services.AddControllersWithViews(options =>
        {
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
                options.Cookie.HttpOnly = true; // JS không đụng cookies
                options.Cookie.SameSite = SameSiteMode.Lax; // Chỉ get hợp lệ từ link
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Chỉ HTTPS

                options.Events = new CookieAuthenticationEvents
                {
                    OnValidatePrincipal = TokenService.ValidateAsync
                };
            })
            .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
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
            
            // Post feature policy: Moderator, Admin, HighAdmin có quyền xử lý bài đăng
            options.AddPolicy("ModeratorOrHigher", policy =>
                policy.RequireRole("HighAdmin", "Admin", "Moderator"));
        });
        
        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        
        // Security headers middleware
        app.Use(async (context, next) =>
        {
            // Cấu hình CSP chặt chẽ
            context.Response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " + 
                "script-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net https://code.jquery.com; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com https://cdnjs.cloudflare.com/; " +
                "img-src 'self' data: https://res.cloudinary.com https://*.googleusercontent.com; " +
                "media-src 'self' https://res.cloudinary.com; " +
                "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                "connect-src 'self' wss: https:; " +
                // Không nhúng web vào iframe (Clickjacking)
                "frame-ancestors 'self';");

            // Các header bảo mật khác nên có
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

            await next();
        });
        
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        
        app.MapStaticAssets();

        // Route mặc định: Home/Index → Chào mừng
        app.MapControllerRoute(
            "default",
            "{controller=Home}/{action=Index}");
        
        // SignalR Hubs
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