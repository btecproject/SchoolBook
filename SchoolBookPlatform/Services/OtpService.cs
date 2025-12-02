using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;
using SendGrid;
using SendGrid.Helpers.Mail;
using Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace SchoolBookPlatform.Services;

public class OtpService
{
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly ILogger<OtpService> _logger;

    public OtpService(AppDbContext db, IConfiguration config, ILogger<OtpService> logger)
    {
        _db = db;
        _logger = logger;
        _config = config;
    }

    public async Task<string> GenerateOtpAsync(User user, string type)
    {
        //Xóa otp cũ
        var oldOtps = _db.OtpCodes
            .Where(o => o.UserId == user.Id
                        && o.Type == type
                        && !o.IsUsed
                        && o.ExpiresAt > DateTime.UtcNow.AddHours(7));
        _db.OtpCodes.RemoveRange(oldOtps);
        await _db.SaveChangesAsync();

        var code = new Random().Next(100000, 999999).ToString();
        var otp = new OtpCode
        {
            UserId = user.Id,
            Code = code,
            Type = type,
            ExpiresAt = DateTime.UtcNow.AddHours(7).AddMinutes(3),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow.AddHours(7)
        };
        _db.OtpCodes.Add(otp);
        await _db.SaveChangesAsync();
        try
        {
            await SendOtpAsync(user, type, code);
            _logger.LogInformation("Gửi OTP {code} thành công cho {EmailOrPhone} qua {Type}",
               code, type == "Email" ? user.Email : user.PhoneNumber, type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi gửi OTP qua {Type}", type);
            throw;
        }

        return code;
    }

    private async Task SendOtpAsync(User user, string type, string code)
    {
        // if (type == "SMS" && !string.IsNullOrWhiteSpace(user.PhoneNumber))
        // {
        //     var sid = _config["Twilio:AccountSid"];
        //     var token = _config["Twilio:AuthToken"];
        //     var from = _config["Twilio:FromPhoneNumber"];
        //
        //     TwilioClient.Init(sid, token);
        //
        //     try
        //     {
        //         var message = await MessageResource.CreateAsync(
        //             body: $"[SchoolBook] OTP: {code}. Effective in 3 minutes!",
        //             from: new PhoneNumber(from),
        //             to: new PhoneNumber(user.PhoneNumber)
        //         );
        //         _logger.LogInformation("SMS send successfully: {Sid}", message.Sid);
        //     }
        //     catch (ApiException ex) when (ex.Code == 21608) // Số chưa verify
        //     {
        //         _logger.LogWarning("SMS fail (21608)");
        //     }
        // }
        if (type == "Email" && !string.IsNullOrWhiteSpace(user.Email))
        {
            await SendEmailOtpAsync(user, code);
        }
        else
        {
            throw new InvalidOperationException(
                type == "SMS" ? "SMS error: PhoneNumber null" : "Email error: Email null"
            );
        }
    }

    private async Task SendEmailOtpAsync(User user, string code)
    {
        var apiKey = _config["SendGrid:ApiKey"];
        var fromEmail = _config["SendGrid:FromEmail"];
        var fromName = _config["SendGrid:FromName"];

        if (string.IsNullOrEmpty(apiKey))
            throw new InvalidOperationException("SendGrid API Key chưa cấu hình.");

        var client = new SendGridClient(apiKey);
        var from = new EmailAddress(fromEmail, fromName);
        var to = new EmailAddress(user.Email);
        var subject = "Mã OTP Xác Thực Đăng Nhập - SchoolBook";

        var htmlContent = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
            <h2 style='text-align: center; color: #007bff;'>SchoolBook Platform</h2>
            <p>Xin chào <strong>{user.Username}</strong>,</p>
            <p>Mã OTP của bạn là:</p>
            <div style='text-align: center; margin: 20px 0;'>
                <span style='font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #007bff;'>
                    {code}
                </span>
            </div>
            <p><strong>Hiệu lực trong 3 phút</strong></p>
            <hr>
            <small style='color: #666;'>
                Nếu bạn không yêu cầu, vui lòng bỏ qua email này.<br>
                Email được gửi tự động, không trả lời.
            </small>
        </div>";

        var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
        var response = await client.SendEmailAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Body.ReadAsStringAsync();
            _logger.LogError("SendGrid lỗi {Status}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"SendGrid lỗi: {response.StatusCode}");
        }

        _logger.LogInformation("Email OTP gửi thành công đến {Email}", user.Email);
    }

    public async Task<bool> VerifyOtpAsync(Guid userId, string code, string type)
    {
        var otp = await _db.OtpCodes.FirstOrDefaultAsync(o =>
            o.UserId == userId &&
            o.Code == code &&
            o.Type == type &&
            !o.IsUsed &&
            o.ExpiresAt > DateTime.UtcNow.AddHours(7)
        );
        if (otp == null) return false;
        otp.IsUsed = true;
        _db.OtpCodes.Update(otp);
        await _db.SaveChangesAsync();
        return true;
    }
}