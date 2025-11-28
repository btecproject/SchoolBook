using CloudinaryDotNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Manager;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services;
[Authorize]
public class ChatService(    
    AppDbContext db,
    Cloudinary cloudinary,
    ILogger<AvatarService> logger)
{
    // ktra user có đăng ký chat chưa (bảng ChatUsers)
    public async Task<bool> IsRegisterChatService(Guid userId)
    {
        var isRegister = await db.ChatUsers.FirstOrDefaultAsync(cu => cu.UserId == userId);
        return isRegister != null;
    }
    
    //đăng ký user vào chatUsers 
    public async Task RegisterNewChatUser(ChatUser chatUser)
    {
        await db.ChatUsers.AddAsync(chatUser);
    }
    
    //xác thực pincode 
    public async Task<bool> VerifyPinCode(Guid userId, string pinCodeHash)
    {
        var user = await db.ChatUsers.FirstOrDefaultAsync(cu => cu.UserId == userId);
        if (!BCrypt.Net.BCrypt.Verify(pinCodeHash, user!.PinCodeHash))
        {
            return false;
        }
        return true;
    }
    
}