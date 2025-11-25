using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services;

public class RecoveryCodeService(
    AppDbContext  db
    )
{
    private const int NumberOfCodes = 10;
    private static readonly Random Random = new Random();
    private const string Chars = "ACEFHJKMNPRTUVWXY34679";

    public async Task<List<string>> GenerateAndSaveNewCodesAsync(Guid userId)
    {
        //Xóa code cũ
        var oldCodes = await db.RecoveryCodes
            .Where(rc => rc.UserId == userId)
            .ToListAsync();
        db.RecoveryCodes.RemoveRange(oldCodes);

        var codes = new List<string>();
        for (int i = 0; i < NumberOfCodes; i++)
        {
            var codeChar = new string(Enumerable.Repeat(Chars, 12)
                .Select(s => s[Random.Next(s.Length)]).ToArray());
            var plainCode = new string(codeChar);
            var formattedCode = $"{plainCode.Substring(0, 4)}-" +
                                $"{plainCode.Substring(4, 4)}-" +
                                $"{plainCode.Substring(8)}";            
            codes.Add(formattedCode);
            var hashedCode = BCrypt.Net.BCrypt.HashPassword(plainCode);

            db.RecoveryCodes.Add(new RecoveryCode
            {
                UserId = userId,
                HashedCode = hashedCode,
                CreatedAt = DateTime.Now,
            });
        }
        await db.SaveChangesAsync();
        return codes;
    }

    public async Task<bool> VerifyCodeAsync(Guid userId, string inputCode)
    {
        var cleanCode = inputCode.Replace("-", "").Replace(" ", "").ToUpperInvariant();

        var unusedCodes = await db.RecoveryCodes
            .Where(rc => rc.UserId == userId && !rc.IsUsed)
            .ToListAsync();

        foreach (var rc in unusedCodes)
        {
            if (BCrypt.Net.BCrypt.Verify(cleanCode, rc.HashedCode))
            {
                rc.IsUsed = true;
                rc.UsedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
                return true;
            }
        }
        return false;
    }

    public async Task<int> GetRemainingCountAsync(Guid userId)
    {
        return await db.RecoveryCodes.CountAsync(rc => rc.UserId == userId && !rc.IsUsed);
    }
}