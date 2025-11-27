using CloudinaryDotNet;
using SchoolBookPlatform.Data;

namespace SchoolBookPlatform.Services;

public class CloudinaryService(
    AppDbContext db,
    Cloudinary cloudinary,
    ILogger<CloudinaryService> logger
    )
{
    private const string SERVICE_NAME = "cloudinary";
    
    
}