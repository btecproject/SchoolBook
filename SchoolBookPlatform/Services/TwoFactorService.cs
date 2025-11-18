using OtpNet;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
namespace SchoolBookPlatform.Services;
public class TwoFactorService(
    ILogger<TwoFactorService> _logger
    )
{
    public string FormatSecretKey(string secret)
    {
        var formatted = "";
        for (int i = 0; i < secret.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
                formatted += " ";
            formatted += secret[i];
        }
        return formatted;
    }
    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        
        var secret = Base32Encoding.ToString(key);
        _logger.LogInformation("Genetaed new secret key");
        return secret;
    }

    public string GenerateQrCodeUri(string username, string secret)
    {
        var issuer = "SchoolBook";
        var uri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:" +
                  $"{Uri.EscapeDataString(username)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";
        return uri;
    }
    
    public string GenerateQrCodeImage(string qrCodeUri)
    {
        using var qrGenerator = new QRCodeGenerator();
        
        using var qrCodeData = qrGenerator.CreateQrCode(qrCodeUri, QRCodeGenerator.ECCLevel.Q);
        
        using var qrCode = new QRCode(qrCodeData);
        using var qrCodeImage = qrCode.GetGraphic(20);
        
        using var ms = new MemoryStream();
        qrCodeImage.Save(ms, ImageFormat.Png);
        var imageBytes = ms.ToArray();
        var base64String = Convert.ToBase64String(imageBytes);
        
        _logger.LogInformation("Generated QR code image");
        return base64String;
    }

    public bool VerifyCode(string secret, string code)
    {
        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            
            // Tạo TOTP object
            var totp = new Totp(secretBytes);
            
            var isValid = totp.VerifyTotp(code, out long timeStepMatched, new VerificationWindow(2, 2));
            
            if (isValid)
            {
                _logger.LogInformation("TOTP code verified successfully at time step {TimeStep}", timeStepMatched);
            }
            else
            {
                _logger.LogWarning("TOTP code verification failed");
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying TOTP code");
            return false;
        }
    }
    
    
}