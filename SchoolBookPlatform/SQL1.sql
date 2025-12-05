-- =======================================================
-- 1. DROP TABLES (Xóa bảng cũ theo thứ tự để tránh lỗi FK)
-- =======================================================
DROP TABLE IF EXISTS MessageNotifications;
DROP TABLE IF EXISTS MessageAttachments;
DROP TABLE IF EXISTS Messages;
DROP TABLE IF EXISTS ConversationKeys;
DROP TABLE IF EXISTS ConversationMembers;
DROP TABLE IF EXISTS Conversations;
DROP TABLE IF EXISTS UserRsaKeys;
DROP TABLE IF EXISTS ChatUsers;
DROP TABLE IF EXISTS PostReports;
DROP TABLE IF EXISTS PostVotes;
DROP TABLE IF EXISTS PostAttachments;
DROP TABLE IF EXISTS PostComments;
DROP TABLE IF EXISTS Posts;
DROP TABLE IF EXISTS Following;
DROP TABLE IF EXISTS Followers;
DROP TABLE IF EXISTS TrustedDevices;
DROP TABLE IF EXISTS FaceProfiles;
DROP TABLE IF EXISTS UserProfiles;
DROP TABLE IF EXISTS RecoveryCodes;
DROP TABLE IF EXISTS OtpCodes;
DROP TABLE IF EXISTS UserTokens;
DROP TABLE IF EXISTS UserRoles;
DROP TABLE IF EXISTS Roles;
DROP TABLE IF EXISTS Users;
DROP PROCEDURE IF EXISTS usp_DeleteUser;
DROP PROCEDURE IF EXISTS DeleteUser;
GO

-- =======================================================
-- 2. CREATE TABLES (CORE SYSTEM)
-- =======================================================

CREATE TABLE Users (
                       Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                       Username NVARCHAR(50) NOT NULL UNIQUE,
                       PasswordHash NVARCHAR(MAX) NOT NULL,
                       Email NVARCHAR(200),
                       PhoneNumber NVARCHAR(20),
                       FaceId NVARCHAR(100),
                       FaceRegistered BIT DEFAULT 0,
                       MustChangePassword BIT DEFAULT 1,
                       TokenVersion INT DEFAULT 1,
                       IsActive BIT DEFAULT 1,
                       CreatedAt DATETIME DEFAULT GETUTCDATE(),
                       UpdatedAt DATETIME NULL,
                       TwoFactorEnabled BIT DEFAULT 0,
                       TwoFactorSecret NVARCHAR(200) NULL,
                       RecoveryCodesGenerated BIT DEFAULT 0,
                       RecoveryCodesLeft INT DEFAULT 0
);

CREATE TABLE Roles (
                       Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                       Name NVARCHAR(50) NOT NULL UNIQUE,
                       Description NVARCHAR(200)
);

CREATE TABLE UserRoles (
                           UserId UNIQUEIDENTIFIER NOT NULL,
                           RoleId UNIQUEIDENTIFIER NOT NULL,
                           PRIMARY KEY (UserId, RoleId),
                           FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                           FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);

CREATE TABLE UserTokens (
                            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                            UserId UNIQUEIDENTIFIER NOT NULL,
                            LoginAt DATETIME DEFAULT GETUTCDATE(),
                            ExpiredAt DATETIME NOT NULL,
                            IsRevoked BIT DEFAULT 0,
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE INDEX IX_UserTokens_UserId ON UserTokens (UserId);

CREATE TABLE OtpCodes (
                          Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                          UserId UNIQUEIDENTIFIER NOT NULL,
                          Code NVARCHAR(10) NOT NULL,
                          Type NVARCHAR(10) CHECK(Type IN ('SMS', 'Email')),
                          ExpiresAt DATETIME NOT NULL,
                          IsUsed BIT DEFAULT 0,
                          CreatedAt DATETIME DEFAULT GETUTCDATE(),
                          FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE RecoveryCodes (
                               Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                               UserId UNIQUEIDENTIFIER NOT NULL,
                               HashedCode NVARCHAR(255) NOT NULL,
                               IsUsed BIT NOT NULL DEFAULT 0,
                               CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
                               UsedAt DATETIME2(7) NULL,
                               FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE NONCLUSTERED INDEX IX_RecoveryCodes_UserId_IsUsed ON RecoveryCodes (UserId, IsUsed) INCLUDE (HashedCode);

-- =======================================================
-- 3. CREATE TABLES (SOCIAL & SECURITY)
-- =======================================================

CREATE TABLE UserProfiles (
                              UserId UNIQUEIDENTIFIER PRIMARY KEY,
                              FullName NVARCHAR(100),
                              Bio NVARCHAR(500),
                              AvatarUrl NVARCHAR(255),
                              Gender NVARCHAR(10) CHECK (Gender IN ('Male', 'Female', 'Other')),
                              BirthDate DATE,
                              IsEmailPublic BIT DEFAULT 0,
                              IsPhonePublic BIT DEFAULT 0,
                              IsBirthDatePublic BIT DEFAULT 0,
                              IsFollowersPublic BIT DEFAULT 1,
                              UpdatedAt DATETIME DEFAULT GETUTCDATE(),
                              FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE FaceProfiles (
                              UserId UNIQUEIDENTIFIER PRIMARY KEY,
                              PersonId NVARCHAR(100),
                              RegisteredAt DATETIME DEFAULT GETUTCDATE(),
                              LastVerifiedAt DATETIME NULL,
                              ConfidenceLast FLOAT NULL,
                              IsLivenessVerified BIT DEFAULT 0,
                              FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE TrustedDevices (
                                Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                                UserId UNIQUEIDENTIFIER NOT NULL,
                                IPAddress NVARCHAR(50) NOT NULL,
                                DeviceInfo NVARCHAR(200) NOT NULL,
                                TrustedAt DATETIME DEFAULT GETUTCDATE(),
                                ExpiresAt DATETIME NOT NULL,
                                IsRevoked BIT DEFAULT 0,
                                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE INDEX IX_TrustedDevices_UserId_IP_Device ON TrustedDevices (UserId, IPAddress, DeviceInfo);

-- Bảng Followers & Following
CREATE TABLE Followers (
                           UserId UNIQUEIDENTIFIER NOT NULL,
                           FollowerId UNIQUEIDENTIFIER NOT NULL,
                           FollowedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
                           CONSTRAINT PK_Followers PRIMARY KEY (UserId, FollowerId),
                           FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                           FOREIGN KEY (FollowerId) REFERENCES Users(Id) -- No Action
);
CREATE NONCLUSTERED INDEX IX_Followers_FollowerId ON Followers(FollowerId);

CREATE TABLE Following (
                           UserId UNIQUEIDENTIFIER NOT NULL,
                           FollowingId UNIQUEIDENTIFIER NOT NULL,
                           FollowedAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
                           CONSTRAINT PK_Following PRIMARY KEY (UserId, FollowingId),
                           FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                           FOREIGN KEY (FollowingId) REFERENCES Users(Id) -- No Action
);
CREATE NONCLUSTERED INDEX IX_Following_FollowingId ON Following(FollowingId);

-- =======================================================
-- 4. CREATE TABLES (POST SYSTEM)
-- =======================================================

-- Bảng Posts
CREATE TABLE Posts (
                       Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                       UserId UNIQUEIDENTIFIER NOT NULL,
                       Title NVARCHAR(300) NOT NULL,
                       Content NVARCHAR(MAX) NOT NULL,
                       CreatedAt DATETIME DEFAULT GETUTCDATE(),
                       UpdatedAt DATETIME NULL,
                       IsDeleted BIT NOT NULL DEFAULT 0,
                       IsVisible BIT NOT NULL DEFAULT 1,
                       VisibleToRoles NVARCHAR(50)
                           CHECK (VisibleToRoles IN ('Student', 'Teacher', 'Admin', 'All')),
                       FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- Index cho Posts
CREATE INDEX IX_Posts_UserId ON Posts(UserId);
CREATE INDEX IX_Posts_CreatedAt ON Posts(CreatedAt DESC);
CREATE INDEX IX_Posts_Visible ON Posts(IsVisible, IsDeleted, VisibleToRoles);

-- Bảng Post Attachments
CREATE TABLE PostAttachments (
                                 Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                                 PostId UNIQUEIDENTIFIER NOT NULL,
                                 FileName NVARCHAR(255) NOT NULL,
                                 FilePath NVARCHAR(500) NOT NULL,
                                 FileSize INT NOT NULL,
                                 UploadedAt DATETIME DEFAULT GETUTCDATE(),
                                 FOREIGN KEY (PostId) REFERENCES Posts(Id) ON DELETE CASCADE
);
CREATE INDEX IX_PostAttachments_PostId ON PostAttachments(PostId);

-- Bảng Post Comments
CREATE TABLE PostComments (
                              Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                              PostId UNIQUEIDENTIFIER NOT NULL,
                              UserId UNIQUEIDENTIFIER NOT NULL,
                              Content NVARCHAR(MAX) NOT NULL,
                              CreatedAt DATETIME DEFAULT GETUTCDATE(),
                              ParentCommentId UNIQUEIDENTIFIER NULL,
                              FOREIGN KEY (PostId) REFERENCES Posts(Id) ON DELETE CASCADE,
                              FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION,
                              FOREIGN KEY (ParentCommentId) REFERENCES PostComments(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_PostComments_PostId ON PostComments(PostId);
CREATE INDEX IX_PostComments_ParentId ON PostComments(ParentCommentId);
CREATE INDEX IX_PostComments_UserId ON PostComments(UserId);

-- Bảng Post Votes (Like/Dislike)
CREATE TABLE PostVotes (
                           PostId UNIQUEIDENTIFIER NOT NULL,
                           UserId UNIQUEIDENTIFIER NOT NULL,
                           VoteType BIT NOT NULL CHECK (VoteType IN (0,1)),
                           VotedAt DATETIME DEFAULT GETUTCDATE(),
                           PRIMARY KEY (PostId, UserId),
                           FOREIGN KEY (PostId) REFERENCES Posts(Id) ON DELETE CASCADE,
                           FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_PostVotes_PostId ON PostVotes(PostId);
CREATE INDEX IX_PostVotes_UserId ON PostVotes(UserId);

-- Bảng Comment Votes
CREATE TABLE CommentVotes (
                              CommentId UNIQUEIDENTIFIER NOT NULL,
                              UserId UNIQUEIDENTIFIER NOT NULL,
                              VoteType BIT NOT NULL CHECK (VoteType IN (0,1)),
                              VotedAt DATETIME DEFAULT GETUTCDATE(),
                              PRIMARY KEY (CommentId, UserId),
                              FOREIGN KEY (CommentId) REFERENCES PostComments(Id) ON DELETE CASCADE,
                              FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_CommentVotes_CommentId ON CommentVotes(CommentId);
CREATE INDEX IX_CommentVotes_UserId ON CommentVotes(UserId);

-- Bảng Post Reports
CREATE TABLE PostReports (
                             Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                             PostId UNIQUEIDENTIFIER NOT NULL,
                             ReportedBy UNIQUEIDENTIFIER NOT NULL,
                             Reason NVARCHAR(500) NOT NULL,
                             Status NVARCHAR(20) NOT NULL DEFAULT 'Pending'
                                 CHECK (Status IN ('Pending', 'Approved', 'Rejected')),
                             ReviewedBy UNIQUEIDENTIFIER NULL,
                             ReviewedAt DATETIME NULL,
                             CreatedAt DATETIME DEFAULT GETUTCDATE(),
                             FOREIGN KEY (PostId) REFERENCES Posts(Id) ON DELETE CASCADE,
                             FOREIGN KEY (ReportedBy) REFERENCES Users(Id) ON DELETE NO ACTION,
                             FOREIGN KEY (ReviewedBy) REFERENCES Users(Id) ON DELETE NO ACTION
);
CREATE INDEX IX_PostReports_PostId ON PostReports(PostId);
CREATE INDEX IX_PostReports_Status ON PostReports(Status);
CREATE INDEX IX_PostReports_ReportedBy ON PostReports(ReportedBy);

-- Bảng Saved Posts
CREATE TABLE SavedPosts (
                            UserId UNIQUEIDENTIFIER NOT NULL,
                            PostId UNIQUEIDENTIFIER NOT NULL,
                            SavedAt DATETIME DEFAULT GETUTCDATE(),
                            PRIMARY KEY (UserId, PostId),
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
                            FOREIGN KEY (PostId) REFERENCES Posts(Id) ON DELETE CASCADE
);
CREATE INDEX IX_SavedPosts_UserId ON SavedPosts(UserId);
CREATE INDEX IX_SavedPosts_PostId ON SavedPosts(PostId);

-- =======================================================
-- 5. CREATE TABLES (CHAT SYSTEM)
-- =======================================================

-- Bảng ChatUsers
CREATE TABLE ChatUsers (
                           Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                           UserId UNIQUEIDENTIFIER NOT NULL,
                           Username NVARCHAR(256) NOT NULL,
                           DisplayName NVARCHAR(100) NOT NULL,
                           PinCodeHash NVARCHAR(256) NOT NULL,
                           IsActive BIT DEFAULT 1,
                           CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                           UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                           CONSTRAINT FK_ChatUsers_UserId FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_ChatUsers_Active ON ChatUsers(UserId) WHERE IsActive = 1;
CREATE UNIQUE INDEX IX_ChatUsers_Username_Active ON ChatUsers(Username) WHERE IsActive = 1;
CREATE INDEX IX_ChatUsers_Search ON ChatUsers (DisplayName);

-- Bảng UserRsaKeys
CREATE TABLE UserRsaKeys (
                             Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                             ChatUserId UNIQUEIDENTIFIER NOT NULL,
                             PublicKey NVARCHAR(MAX) NOT NULL,
                             PrivateKeyEncrypted NVARCHAR(MAX) NOT NULL,
                             CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                             ExpiresAt DATETIME2 NOT NULL,
                             IsActive BIT NOT NULL DEFAULT 1,
                             CONSTRAINT FK_UserRsaKeys_ChatUserId FOREIGN KEY (ChatUserId) REFERENCES ChatUsers(Id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX IX_UserRsaKeys_Active_User ON UserRsaKeys (ChatUserId) WHERE IsActive = 1;
CREATE INDEX IX_UserRsaKeys_ExpiresAt ON UserRsaKeys (ExpiresAt);

-- Bảng Conversations
CREATE TABLE Conversations (
                               Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                               Type TINYINT NOT NULL,
                               Name NVARCHAR(100) NULL,
                               Avatar NVARCHAR(500) NULL,
                               CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
                               CreatorId UNIQUEIDENTIFIER NULL,
                               FOREIGN KEY (CreatorId) REFERENCES Users(Id) ON DELETE SET NULL
);

-- Bảng ConversationMembers
CREATE TABLE ConversationMembers (
                                     ConversationId UNIQUEIDENTIFIER NOT NULL,
                                     ChatUserId UNIQUEIDENTIFIER NOT NULL,
                                     JoinedAt DATETIME2 DEFAULT GETUTCDATE(),
                                     Role TINYINT DEFAULT 0,
                                     CONSTRAINT PK_ConversationMembers PRIMARY KEY (ConversationId, ChatUserId),
                                     FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE,
                                     FOREIGN KEY (ChatUserId) REFERENCES ChatUsers(Id) ON DELETE CASCADE
);

-- Bảng ConversationKeys
CREATE TABLE ConversationKeys (
                                  ChatUserId UNIQUEIDENTIFIER NOT NULL,
                                  ConversationId UNIQUEIDENTIFIER NOT NULL,
                                  KeyVersion INT NOT NULL DEFAULT 1,
                                  EncryptedKey NVARCHAR(MAX) NOT NULL,
                                  UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),
                                  CONSTRAINT PK_ConversationKeys PRIMARY KEY (ChatUserId, ConversationId, KeyVersion),
                                  FOREIGN KEY (ChatUserId) REFERENCES ChatUsers(Id) ON DELETE CASCADE,
                                  FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE
);

-- Bảng Messages
CREATE TABLE Messages (
                          Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                          ConversationId UNIQUEIDENTIFIER NOT NULL,
                          SenderId UNIQUEIDENTIFIER NOT NULL,
                          MessageType TINYINT NOT NULL,
                          CipherText NVARCHAR(MAX) NOT NULL,
                          ReplyToId BIGINT NULL,
                          CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
                          FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE,
                          FOREIGN KEY (SenderId) REFERENCES ChatUsers(Id),
                          FOREIGN KEY (ReplyToId) REFERENCES Messages(Id)
);
CREATE INDEX IX_Conv_Created ON Messages (ConversationId, CreatedAt DESC);
CREATE INDEX IX_Sender_Created ON Messages (SenderId, CreatedAt DESC);

-- Bảng MessageAttachments
CREATE TABLE MessageAttachments (
                                    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                                    MessageId BIGINT NOT NULL,
                                    CloudinaryUrl NVARCHAR(1000) NOT NULL,
                                    ResourceType NVARCHAR(20) NOT NULL,
                                    Format NVARCHAR(20) NOT NULL,
                                    FileName NVARCHAR(255) NULL,
                                    UploadedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
                                    FOREIGN KEY (MessageId) REFERENCES Messages(Id) ON DELETE CASCADE
);

-- Bảng MessageNotifications
CREATE TABLE MessageNotifications (
                                      RecipientId UNIQUEIDENTIFIER NOT NULL,
                                      SenderId UNIQUEIDENTIFIER NOT NULL,
                                      UnreadCount INT NOT NULL DEFAULT 1,
                                      LastMessageId BIGINT NULL,
                                      LastSentAt DATETIME2 DEFAULT GETUTCDATE(),
                                      PRIMARY KEY (RecipientId, SenderId),
                                      FOREIGN KEY (RecipientId) REFERENCES ChatUsers(Id),
                                      FOREIGN KEY (SenderId) REFERENCES ChatUsers(Id),
                                      FOREIGN KEY (LastMessageId) REFERENCES Messages(Id) ON DELETE CASCADE
);
CREATE INDEX IX_Recipient_Unread ON MessageNotifications (RecipientId, UnreadCount DESC);
GO

-- =======================================================
-- 6. STORED PROCEDURES
-- =======================================================

-- Procedure để xóa người dùng
CREATE OR ALTER PROCEDURE DeleteUser
    @UserId UNIQUEIDENTIFIER
    AS
BEGIN
    SET NOCOUNT ON;
BEGIN TRANSACTION;
    
    -- 1. Xoá Saved Posts
DELETE FROM SavedPosts WHERE UserId = @UserId;

-- 2. Xoá Comment Votes (của người dùng)
DELETE FROM CommentVotes WHERE UserId = @UserId;

-- 3. Xoá Post Votes (của người dùng)
DELETE FROM PostVotes WHERE UserId = @UserId;

-- 4. Xoá Post Reports (do người dùng tạo)
DELETE FROM PostReports WHERE ReportedBy = @UserId;

-- 5. Xoá comment con (reply) của người dùng
;WITH CommentTree AS (
    SELECT Id
    FROM PostComments
    WHERE UserId = @UserId

    UNION ALL

    SELECT pc.Id
    FROM PostComments pc
             INNER JOIN CommentTree ct ON pc.ParentCommentId = ct.Id
)
DELETE FROM PostComments
WHERE Id IN (SELECT Id FROM CommentTree);

-- 6. Xoá comments của user trực tiếp
DELETE FROM PostComments WHERE UserId = @UserId;

-- 7. Xoá comment thuộc post mà user sở hữu
DELETE FROM PostComments
WHERE PostId IN (
    SELECT Id FROM Posts WHERE UserId = @UserId
);

-- 8. Xoá vote comments (trên comment của user)
DELETE FROM CommentVotes
WHERE CommentId IN (
    SELECT Id FROM PostComments WHERE UserId = @UserId
);

-- 9. Xoá vote posts (trên post của user)
DELETE FROM PostVotes
WHERE PostId IN (
    SELECT Id FROM Posts WHERE UserId = @UserId
);

-- 10. Xoá attachments của post
DELETE FROM PostAttachments
WHERE PostId IN (
    SELECT Id FROM Posts WHERE UserId = @UserId
);

-- 11. Xoá posts
DELETE FROM Posts WHERE UserId = @UserId;

-- 12. Xoá Post Reports (liên quan đến post của user)
DELETE FROM PostReports
WHERE PostId IN (
    SELECT Id FROM Posts WHERE UserId = @UserId
);

-- 13. Xoá dữ liệu chat
DELETE FROM Followers WHERE FollowerId = @UserId OR UserId = @UserId;
DELETE FROM Following WHERE FollowingId = @UserId OR UserId = @UserId;

-- 14. Cuối cùng: xoá user (cascade sẽ xoá các bảng liên quan)
DELETE FROM Users WHERE Id = @UserId;

COMMIT TRANSACTION;
END
GO

-- Procedure đơn giản hơn (alternative)
CREATE OR ALTER PROCEDURE usp_DeleteUser
    @userId UNIQUEIDENTIFIER
    AS
BEGIN
    SET NOCOUNT ON;
BEGIN TRAN;
    
    -- Xóa các relationship trước khi xóa user
DELETE FROM Followers WHERE FollowerId = @userId;
DELETE FROM Following WHERE FollowingId = @userId;

-- Xóa user (cascade sẽ xử lý các bảng khác)
DELETE FROM Users WHERE Id = @userId;

COMMIT TRAN;
END
GO

-- Procedure để lấy bài viết với phân trang
CREATE OR ALTER PROCEDURE GetPostsWithPagination
    @PageNumber INT = 1,
    @PageSize INT = 10,
    @UserId UNIQUEIDENTIFIER = NULL,
    @RoleName NVARCHAR(50) = NULL
    AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    -- Lấy role của user nếu có
    DECLARE @UserRole NVARCHAR(50);
    IF @UserId IS NOT NULL
BEGIN
SELECT TOP 1 @UserRole = r.Name
FROM UserRoles ur
         JOIN Roles r ON ur.RoleId = r.Id
WHERE ur.UserId = @UserId
ORDER BY CASE r.Name
             WHEN 'HighAdmin' THEN 1
             WHEN 'Admin' THEN 2
             WHEN 'Moderator' THEN 3
             WHEN 'Teacher' THEN 4
             WHEN 'Student' THEN 5
             ELSE 6
             END;
END
    
    -- Main query
SELECT
    p.Id,
    p.UserId,
    u.Username,
    up.FullName,
    up.AvatarUrl,
    p.Title,
    p.Content,
    p.CreatedAt,
    p.UpdatedAt,
    p.VisibleToRoles,
    -- Đếm số lượng comments
    (SELECT COUNT(*) FROM PostComments pc WHERE pc.PostId = p.Id) AS CommentCount,
    -- Đếm số lượng likes
    (SELECT COUNT(*) FROM PostVotes pv WHERE pv.PostId = p.Id AND pv.VoteType = 1) AS LikeCount,
    -- Đếm số lượng dislikes
    (SELECT COUNT(*) FROM PostVotes pv WHERE pv.PostId = p.Id AND pv.VoteType = 0) AS DislikeCount,
    -- Kiểm tra user đã vote chưa
    CASE WHEN @UserId IS NOT NULL THEN
             (SELECT TOP 1 VoteType FROM PostVotes WHERE PostId = p.Id AND UserId = @UserId)
         ELSE NULL END AS UserVote,
    -- Kiểm tra user đã save chưa
    CASE WHEN @UserId IS NOT NULL THEN
             (SELECT COUNT(*) FROM SavedPosts WHERE PostId = p.Id AND UserId = @UserId)
         ELSE 0 END AS IsSaved,
    -- Đếm số lượng attachments
    (SELECT COUNT(*) FROM PostAttachments pa WHERE pa.PostId = p.Id) AS AttachmentCount
FROM Posts p
         JOIN Users u ON p.UserId = u.Id
         LEFT JOIN UserProfiles up ON u.Id = up.UserId
WHERE p.IsDeleted = 0
  AND p.IsVisible = 1
  AND (
    -- Điều kiện hiển thị theo role
    p.VisibleToRoles = 'All'
        OR (@UserRole IS NOT NULL AND p.VisibleToRoles = @UserRole)
        OR (@UserRole IN ('HighAdmin', 'Admin', 'Moderator')) -- Admin xem được tất cả
        OR (p.UserId = @UserId) -- User xem được bài của chính mình
    )
ORDER BY p.CreatedAt DESC
OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

-- Đếm tổng số bài viết
SELECT COUNT(*) AS TotalCount
FROM Posts p
WHERE p.IsDeleted = 0
  AND p.IsVisible = 1
  AND (
    p.VisibleToRoles = 'All'
        OR (@UserRole IS NOT NULL AND p.VisibleToRoles = @UserRole)
        OR (@UserRole IN ('HighAdmin', 'Admin', 'Moderator'))
        OR (p.UserId = @UserId)
    );
END
GO

-- Procedure để lấy comments của một post
CREATE OR ALTER PROCEDURE GetPostComments
    @PostId UNIQUEIDENTIFIER,
    @ParentCommentId UNIQUEIDENTIFIER = NULL
    AS
BEGIN
    SET NOCOUNT ON;

WITH CommentCTE AS (
    SELECT
        pc.Id,
        pc.PostId,
        pc.UserId,
        u.Username,
        up.FullName,
        up.AvatarUrl,
        pc.Content,
        pc.CreatedAt,
        pc.ParentCommentId,
        -- Đếm số like/dislike
        (SELECT COUNT(*) FROM CommentVotes cv WHERE cv.CommentId = pc.Id AND cv.VoteType = 1) AS LikeCount,
        (SELECT COUNT(*) FROM CommentVotes cv WHERE cv.CommentId = pc.Id AND cv.VoteType = 0) AS DislikeCount,
        0 AS Level -- Level 0 cho comment gốc
    FROM PostComments pc
             JOIN Users u ON pc.UserId = u.Id
             LEFT JOIN UserProfiles up ON u.Id = up.UserId
    WHERE pc.PostId = @PostId
      AND pc.ParentCommentId = @ParentCommentId

    UNION ALL

    SELECT
        pc.Id,
        pc.PostId,
        pc.UserId,
        u.Username,
        up.FullName,
        up.AvatarUrl,
        pc.Content,
        pc.CreatedAt,
        pc.ParentCommentId,
        (SELECT COUNT(*) FROM CommentVotes cv WHERE cv.CommentId = pc.Id AND cv.VoteType = 1) AS LikeCount,
        (SELECT COUNT(*) FROM CommentVotes cv WHERE cv.CommentId = pc.Id AND cv.VoteType = 0) AS DislikeCount,
        cte.Level + 1 AS Level
    FROM PostComments pc
             JOIN Users u ON pc.UserId = u.Id
             LEFT JOIN UserProfiles up ON u.Id = up.UserId
             INNER JOIN CommentCTE cte ON pc.ParentCommentId = cte.Id
    WHERE pc.PostId = @PostId
)
SELECT * FROM CommentCTE
ORDER BY
    CASE WHEN ParentCommentId IS NULL THEN CreatedAt END,
    ParentCommentId,
    CreatedAt;
END
GO

-- =======================================================
-- 7. SEED DATA
-- =======================================================

-- Thêm roles
INSERT INTO Roles (Name, Description) VALUES
('HighAdmin', 'Super admin with full control'),
('Admin', 'System administrator'),
('Moderator', 'Content moderator'),
('Teacher', 'Content creator'),
('Student', 'Basic user');

-- Thêm user highadmin
DECLARE @highAdminId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Users (Id, Username, PasswordHash, Email, PhoneNumber, FaceRegistered, MustChangePassword, TokenVersion, IsActive)
VALUES (@highAdminId, 'highadmin', '$2a$12$1z0WFrouH5JZdDkmpjQPiuyOcYIOeswMPhJMDa7VwJe9uT/d0QoD.', 'highadmin@mail.com', '0123456789', 0, 1, 1, 1);

-- Gán role cho highadmin
INSERT INTO UserRoles (UserId, RoleId)
SELECT @highAdminId, Id FROM Roles WHERE Name = 'HighAdmin';

-- Thêm profile cho highadmin
INSERT INTO UserProfiles (UserId, FullName, Bio, AvatarUrl, Gender)
VALUES (@highAdminId, 'Super Admin', 'System Administrator', NULL, 'Other');

-- Thêm một số user mẫu cho testing
DECLARE @teacherId UNIQUEIDENTIFIER = NEWID();
DECLARE @studentId UNIQUEIDENTIFIER = NEWID();

INSERT INTO Users (Id, Username, PasswordHash, Email, PhoneNumber, IsActive) VALUES
                                                                                 (@teacherId, 'teacher1', '$2a$12$1z0WFrouH5JZdDkmpjQPiuyOcYIOeswMPhJMDa7VwJe9uT/d0QoD.', 'teacher1@mail.com', '0987654321', 1),
                                                                                 (@studentId, 'student1', '$2a$12$1z0WFrouH5JZdDkmpjQPiuyOcYIOeswMPhJMDa7VwJe9uT/d0QoD.', 'student1@mail.com', '0123456780', 1);

INSERT INTO UserRoles (UserId, RoleId) VALUES
                                           (@teacherId, (SELECT Id FROM Roles WHERE Name = 'Teacher')),
                                           (@studentId, (SELECT Id FROM Roles WHERE Name = 'Student'));

INSERT INTO UserProfiles (UserId, FullName, Bio) VALUES
                                                     (@teacherId, 'John Teacher', 'Mathematics Teacher'),
                                                     (@studentId, 'Alice Student', 'Computer Science Student');

-- Thêm một số bài viết mẫu
INSERT INTO Posts (Id, UserId, Title, Content, VisibleToRoles) VALUES
                                                                   (NEWID(), @teacherId, 'Welcome to Mathematics Class', 'This semester we will cover calculus and linear algebra. Please check the syllabus for more details.', 'All'),
                                                                   (NEWID(), @teacherId, 'Assignment 1 Posted', 'Assignment 1 is now available on the portal. Deadline: Next Friday.', 'Student'),
                                                                   (NEWID(), @highAdminId, 'System Maintenance', 'The system will undergo maintenance this weekend. Please save your work.', 'All'),
                                                                   (NEWID(), @studentId, 'Study Group Meeting', 'Looking for students to form a study group for advanced programming.', 'Student');

GO

-- =======================================================
-- 8. VIEWS (Optional)
-- =======================================================

-- View để xem thông tin bài viết chi tiết
CREATE OR ALTER VIEW vw_PostDetails AS
SELECT
    p.Id,
    p.UserId,
    u.Username,
    up.FullName,
    up.AvatarUrl,
    p.Title,
    p.Content,
    p.CreatedAt,
    p.UpdatedAt,
    p.VisibleToRoles,
    p.IsDeleted,
    p.IsVisible,
    (SELECT COUNT(*) FROM PostComments pc WHERE pc.PostId = p.Id) AS CommentCount,
    (SELECT COUNT(*) FROM PostVotes pv WHERE pv.PostId = p.Id AND pv.VoteType = 1) AS LikeCount,
    (SELECT COUNT(*) FROM PostVotes pv WHERE pv.PostId = p.Id AND pv.VoteType = 0) AS DislikeCount,
    (SELECT COUNT(*) FROM PostAttachments pa WHERE pa.PostId = p.Id) AS AttachmentCount,
    (SELECT COUNT(*) FROM PostReports pr WHERE pr.PostId = p.Id AND pr.Status = 'Pending') AS PendingReports
FROM Posts p
         JOIN Users u ON p.UserId = u.Id
         LEFT JOIN UserProfiles up ON u.Id = up.UserId
WHERE p.IsDeleted = 0;
GO

-- View để xem thống kê người dùng
CREATE OR ALTER VIEW vw_UserStats AS
SELECT
    u.Id,
    u.Username,
    u.Email,
    u.IsActive,
    up.FullName,
    r.Name AS RoleName,
    (SELECT COUNT(*) FROM Posts p WHERE p.UserId = u.Id AND p.IsDeleted = 0) AS PostCount,
    (SELECT COUNT(*) FROM PostComments pc WHERE pc.UserId = u.Id) AS CommentCount,
    (SELECT COUNT(*) FROM Followers f WHERE f.UserId = u.Id) AS FollowerCount,
    (SELECT COUNT(*) FROM Following f WHERE f.UserId = u.Id) AS FollowingCount
FROM Users u
         LEFT JOIN UserProfiles up ON u.Id = up.UserId
         LEFT JOIN UserRoles ur ON u.Id = ur.UserId
         LEFT JOIN Roles r ON ur.RoleId = r.Id;
GO

PRINT 'Database schema created successfully with Post system!';