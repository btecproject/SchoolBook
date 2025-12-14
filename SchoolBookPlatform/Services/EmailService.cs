using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace SchoolBookPlatform.Services;

public class EmailService (AppDbContext db, IConfiguration config, ILogger<OtpService> logger)
{
    private readonly string? _apiKey = config["SendGrid:ApiKey"];
    private readonly string? _fromEmail = config["SendGrid:FromEmail"];
    private readonly string? _fromName = config["SendGrid:FromName"];
    private readonly string webUrl = "https://localhost:7093";
    
    public async Task SendEmailOtpAsync(User user, string code)
    {
        var client = new SendGridClient(_apiKey);
        var from = new EmailAddress(_fromEmail, _fromName);
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
            logger.LogError("SendGrid lỗi {Status}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"SendGrid lỗi: {response.StatusCode}");
        }

        logger.LogInformation("Email OTP gửi thành công đến {Email}", user.Email);
    }
    
    public async Task SendLoginInfoToEmail(string email, string username, string password)
    {
        var client = new SendGridClient(_apiKey);
        var from = new EmailAddress(_fromEmail, _fromName);
        var to = new EmailAddress(email);
        var subject = "Thông tin đăng Nhập - SchoolBook";

        var htmlContent = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
            <h2 style='text-align: center; color: #007bff;'>SchoolBook Platform</h2>
            <p>Xin chào <strong>{email}</strong>,</p>
            <p>Thông tin đăng nhập của bạn:</p>
            <div style='text-align: center; margin: 20px 0;'>
                <span style='font-size: 32px; font-weight: bold; letter-spacing: 5px; color: #007bff;'>
                    Username: {username}<br>
                    Password: {password}<br>
                </span>
            </div>
            <hr>
            <small style='color: #666;'>
                Vui lòng bảo quản kỹ thông tin đăng nhập của mình ! <br>
                Email được gửi tự động, không trả lời.
            </small>
        </div>";

        var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
        var response = await client.SendEmailAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Body.ReadAsStringAsync();
            logger.LogError("SendGrid lỗi {Status}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"SendGrid lỗi: {response.StatusCode}");
        }

        logger.LogInformation("Email Login Info gửi thành công đến {Email}",email);
    }

    public async Task SendWarningEmail(Guid userId, string note)
    {
        var toEmail = await db.GetEmailByIdAsync(userId);
        var userName = await db.GetUserNameByIdAsync(userId);
        var client = new SendGridClient(_apiKey);
        var from = new EmailAddress(_fromEmail, _fromName);
        var to = new EmailAddress(toEmail);
        var subject = "Cảnh báo! - SchoolBook";
        var htmlContent = $@"
        <!DOCTYPE html>
        <html lang='vi'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background-color: #dc3545; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
                .content {{ background-color: #f8f9fa; padding: 30px; border: 1px solid #dee2e6; border-top: none; }}
                .warning {{ background-color: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 4px; margin: 20px 0; }}
                .footer {{ text-align: center; color: #6c757d; font-size: 12px; margin-top: 30px; padding-top: 20px; border-top: 1px solid #dee2e6; }}
                .note-box {{ background-color: #e9ecef; padding: 15px; border-left: 4px solid #6c757d; margin: 15px 0; }}
                .btn {{ display: inline-block; padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 4px; margin-top: 10px; }}
            </style>
        </head>
        <body>
            <div class='header'>
                <h2>⚠ CẢNH BÁO VI PHẠM - SchoolBook</h2>
            </div>
            
            <div class='content'>
                <p>Kính gửi người dùng {userName},</p>
                
                <div class='warning'>
                    <h3>THÔNG BÁO VI PHẠM NỘI QUY SỬ DỤNG</h3>
                    <p>Tài khoản của bạn đã vi phạm <strong>Điều khoản sử dụng</strong> của nền tảng SchoolBook về việc gửi tin nhắn không phù hợp.</p>
                </div>
                
                <p>Chi tiết vi phạm:</p>
                <div class='note-box'>
                    <p><strong>Nội dung cảnh báo:</strong></p>
                    <p>{note}</p>
                </div>
                
                <h3>Hậu quả có thể xảy ra:</h3>
                <ul>
                    <li>Cảnh báo lần 1: Ghi nhận vi phạm</li>
                    <li>Cảnh báo lần 2: Hạn chế quyền sử dụng</li>
                    <li>Cảnh báo lần 3: Khóa tài khoản tạm thời</li>
                    <li>Tái phạm nhiều lần: Khóa tài khoản vĩnh viễn</li>
                </ul>
                
                <h3>Để tránh bị xử lý tiếp theo:</h3>
                <ol>
                    <li>Tuân thủ <a href='{webUrl}/Term'>Quy tắc cộng đồng</a> của SchoolBook</li>
                    <li>Không gửi tin nhắn có nội dung không phù hợp</li>
                    <li>Tôn trọng các thành viên khác trong cộng đồng</li>
                    <li>Liên hệ ban quản trị nếu có thắc mắc</li>
                </ol>
                
                <p>Nếu bạn cho rằng đây là sự nhầm lẫn, vui lòng liên hệ với chúng tôi trong vòng 48 giờ.</p>
                
                <a href='#' class='btn'>KHIẾU NẠI CẢNH BÁO</a>
                
                <p>Trân trọng,<br>
                <strong>Đội ngũ Quản trị SchoolBook</strong></p>
            </div>
            
            <div class='footer'>
                <p>© {DateTime.Now.Year} SchoolBook. Mọi quyền được bảo lưu.</p>
                <p>Đây là email tự động, vui lòng không trả lời email này.</p>
                <p>Liên hệ hỗ trợ: support@schoolbook.edu.vn | Hotline: 1900 1234</p>
            </div>
        </body>
        </html>";
        var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
        var response = await client.SendEmailAsync(msg);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Body.ReadAsStringAsync();
            logger.LogError("SendGrid lỗi {Status}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"SendGrid lỗi: {response.StatusCode}");
        }

        logger.LogInformation("Email Warning gửi thành công đến {Email}", toEmail);
    }
}