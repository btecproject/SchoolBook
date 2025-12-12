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

public class OtpService(AppDbContext db, IConfiguration config, ILogger<OtpService> logger, EmailService emailService)
{
    public async Task<string> GenerateOtpAsync(User user, string type)
    {
        //Xóa otp cũ
        var oldOtps = db.OtpCodes
            .Where(o => o.UserId == user.Id
                        && o.Type == type
                        && !o.IsUsed
                        && o.ExpiresAt > DateTime.UtcNow.AddHours(7));
        db.OtpCodes.RemoveRange(oldOtps);
        await db.SaveChangesAsync();

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
        db.OtpCodes.Add(otp);
        await db.SaveChangesAsync();
        try
        {
            await SendOtpAsync(user, type, code);
            logger.LogInformation("Gửi OTP {code} thành công cho {EmailOrPhone} qua {Type}",
               code, type == "Email" ? user.Email : user.PhoneNumber, type);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi gửi OTP qua {Type}", type);
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
        await emailService.SendEmailOtpAsync(user, code);
    }

    public async Task<bool> VerifyOtpAsync(Guid userId, string code, string type)
    {
        var otp = await db.OtpCodes.FirstOrDefaultAsync(o =>
            o.UserId == userId &&
            o.Code == code &&
            o.Type == type &&
            !o.IsUsed &&
            o.ExpiresAt > DateTime.UtcNow.AddHours(7)
        );
        if (otp == null) return false;
        otp.IsUsed = true;
        db.OtpCodes.Update(otp);
        await db.SaveChangesAsync();
        return true;
    }
}