using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace SchoolBookPlatform.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration config)
    {
        var cloud = config["Cloudinary:CloudName"];
        var key = config["Cloudinary:ApiKey"];
        var secret = config["Cloudinary:ApiSecret"];

        var account = new Account(cloud, key, secret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        await using var stream = file.OpenReadStream();

        var uploadParams = new ImageUploadParams()
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "SchoolBook"   // lưu gọn theo thư mục trên Cloudinary
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        return uploadResult.SecureUrl.ToString();
    }
}