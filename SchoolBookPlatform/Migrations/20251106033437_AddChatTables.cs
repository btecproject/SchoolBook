using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SchoolBookPlatform.Migrations
{
    /// <inheritdoc />
    public partial class AddChatTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Không làm gì cả vì bảng đã tồn tại
            // Hoặc dùng SQL để check trước khi tạo
    
            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatThreads')
        BEGIN
            CREATE TABLE ChatThreads (
                Id INT PRIMARY KEY IDENTITY(1,1),
                ThreadName NVARCHAR(200) NOT NULL,
                UserIds NVARCHAR(MAX) NOT NULL
            );
        END
    ");

            migrationBuilder.Sql(@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatSegments')
        BEGIN
            CREATE TABLE ChatSegments (
                Id INT PRIMARY KEY IDENTITY(1,1),
                ThreadId INT NOT NULL,
                StartTime DATETIME2 NOT NULL,
                EndTime DATETIME2 NULL,
                MessagesJson NVARCHAR(MAX) NOT NULL,
                IsProtected BIT NOT NULL DEFAULT 0,
                PinHash NVARCHAR(500) NULL,
                Salt VARBINARY(16) NULL,
                CONSTRAINT FK_ChatSegments_ChatThreads FOREIGN KEY (ThreadId) 
                    REFERENCES ChatThreads(Id) ON DELETE CASCADE
            );
            
            CREATE INDEX IX_ChatSegments_ThreadId ON ChatSegments(ThreadId);
        END
    ");
        }
    }
}
