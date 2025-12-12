using Microsoft.EntityFrameworkCore;
using SchoolBookPlatform.Data;
using SchoolBookPlatform.DTOs;
using SchoolBookPlatform.Models;

namespace SchoolBookPlatform.Services;

public class MessageReportService(AppDbContext db,
    ChatService chatService,
    EmailService emailService,
    ILogger<MessageReportService> logger
    )
{
    //create report
    public async Task<ServiceResult> CreateReportAsync(Guid reporterId, CreateMessageReportRequest request)
    {
        try
        {
            var message = await db.Messages.Include(m => m.Sender) //sender: chatUser
                .FirstOrDefaultAsync(m => m.Id == request.MessageId);
            if (message == null) return new ServiceResult() { Success = false, Message = "Message not found" };

            var report = new MessageReport
            {
                Id = Guid.NewGuid(),
                MessageId = request.MessageId,
                ReporterId = reporterId,
                ReportedUserId = message.Sender.UserId,
                Reason = request.Reason,
                Details = request.Details,
                DecryptedContent = request.DecryptedContent,
                FileUrl = request.DecryptedFileUrl,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow.AddHours(7)
            };
            db.MessageReports.Add(report);
            await db.SaveChangesAsync();
            return new ServiceResult()
            {
                Success = true,
                Message = "Message report created",
            };
        }
        catch (Exception ex)
        {
            logger.LogError("MessageReportService: "+ex.Message);
            return new ServiceResult()
            {
                Success = false,
                Message = "Message report creation failed"
            };
        }
    }

    //get pending report
// Trả về Tuple gồm: (Danh sách báo cáo, Tổng số lượng)
    public async Task<(List<MessageReport> Reports, int TotalCount)> GetPendingReportsAsync(int pageIndex, int pageSize)
    {
        var query = db.MessageReports
            .Include(r => r.Reporter)    
            .Include(r => r.ReportedUser) 
            .Where(r => r.Status == "Pending");
        
        var totalCount = await query.CountAsync();
        
        var reports = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (reports, totalCount);
    }
    
    //resolve report
    public async Task<ServiceResult> ResolveReportAsync(Guid moderatorId, ResolveReportRequest request)
    {
        var report = await db.MessageReports.FindAsync(request.ReportId);

        if (report == null)
            return new ServiceResult()
            {
                Success = false,
                Message = "Report not found"
            };
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            report.ResolvedBy = moderatorId;
            report.ResolvedAt = DateTime.UtcNow.AddHours(7);
            report.ResolutionNotes = request.Notes;
            if (request.Action == "Deny")
            {
                report.Status = "Denied";
            }
            else
            {
                report.Status = "Resolved";
                if (request.Action == "DeleteMessage" || request.Action == "WarnAndDelete")
                {
                    var msg = await db.Messages.FindAsync(report.MessageId);
                    if (msg != null)
                    {
                        //xóa attachments (nếu có)
                        var attachments = await db.MessageAttachments.Where(a => a.MessageId == msg.Id).ToListAsync();
                        if(attachments.Any()) db.MessageAttachments.RemoveRange(attachments);
                        
                        db.Messages.Remove(msg);
                    }
                }

                if (request.Action == "WarnUser" || request.Action == "WarnAndDelete")
                {
                    var warning = new UserWarning()
                    {
                        UserId = report.ReportedUserId,
                        Reason = $"Vi phạm tin nhắn (Report ID: {report.Id}): {request.Notes}",
                        WarnedBy = moderatorId,
                        CreatedAt = DateTime.UtcNow.AddHours(7)
                    };
                    db.UserWarnings.Add(warning);
                    await emailService.SendWarningEmail(report.ReportedUserId, request.Notes ?? "");
                }
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return new ServiceResult()
            {
                Success = true,
                Message = "Message report resolved"
            };
        }catch (Exception ex)
        {
            await transaction.RollbackAsync();
            logger.LogError("MessageReportService: " + ex.Message);
            return new ServiceResult (){ Success = false, Message = "Resolving error" };        }
    }
}