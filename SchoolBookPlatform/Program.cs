using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
        builder.Services.AddSignalR();
        builder.Services.AddScoped<ChatService>();
        builder.Services.AddSingleton<EncryptionService>();
        builder.Services.AddControllersWithViews();
        
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });
        
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    // Cấu hình ValidateIssuer, ValidateAudience, IssuerSigningKey, v.v.
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    // Thêm key và issuer/audience nếu có
                    // IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("your-secret-key")),
                    // ValidIssuer = "your-issuer",
                    // ValidAudience = "your-audience"
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Path.Value.StartsWith("/hubs/chat") && context.Request.Query.TryGetValue("access_token", out var token))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    }
                };
            });
        
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
        app.UseCors("AllowAll");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapHub<ChatHub>("/chatHub");
        app.MapControllers();

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