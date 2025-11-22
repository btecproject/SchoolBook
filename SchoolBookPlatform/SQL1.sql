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
                       UpdatedAt DATETIME NULL
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
                           FOREIGN KEY (UserId) REFERENCES Users(Id),
                           FOREIGN KEY (RoleId) REFERENCES Roles(Id)
);

CREATE TABLE UserTokens (
                            Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                            UserId UNIQUEIDENTIFIER NOT NULL,
                            LoginAt DATETIME DEFAULT GETUTCDATE(),
                            ExpiredAt DATETIME NOT NULL, -- Hết hạn sau 7 ngày
                            IsRevoked BIT DEFAULT 0,
                            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);
-- Index để tìm nhanh
CREATE INDEX IX_UserTokens_UserId ON UserTokens (UserId);

CREATE TABLE OtpCodes (
                          Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                          UserId UNIQUEIDENTIFIER NOT NULL,
                          Code NVARCHAR(10) NOT NULL,
                          Type NVARCHAR(10) CHECK(Type IN ('SMS', 'Email')),
                          ExpiresAt DATETIME NOT NULL,
                          IsUsed BIT DEFAULT 0,
                          CreatedAt DATETIME DEFAULT GETUTCDATE(),
                          FOREIGN KEY (UserId) REFERENCES Users(Id)
);

CREATE TABLE FaceProfiles (
                              UserId UNIQUEIDENTIFIER PRIMARY KEY,
                              PersonId NVARCHAR(100),
                              RegisteredAt DATETIME DEFAULT GETUTCDATE(),
                              LastVerifiedAt DATETIME NULL,
                              ConfidenceLast FLOAT NULL,
                              IsLivenessVerified BIT DEFAULT 0,
                              FOREIGN KEY (UserId) REFERENCES Users(Id)
);
CREATE TABLE TrustedDevices (
                                Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                                UserId UNIQUEIDENTIFIER NOT NULL,
                                IPAddress NVARCHAR(50) NOT NULL,
                                DeviceInfo NVARCHAR(200) NOT NULL,
                                TrustedAt DATETIME DEFAULT GETUTCDATE(),
                                ExpiresAt DATETIME NOT NULL, -- Hết hạn sau 30 ngày
                                IsRevoked BIT DEFAULT 0,
                                FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- Index để tìm nhanh
CREATE INDEX IX_TrustedDevices_UserId_IP_Device
    ON TrustedDevices (UserId, IPAddress, DeviceInfo);

INSERT INTO Roles (Name, Description)
VALUES
    ('HighAdmin', 'Super admin with full control'),
    ('Admin', 'System administrator'),
    ('Moderator', 'Content moderator'),
    ('Teacher', 'Content creator'),
    ('Student', 'Basic user');


DECLARE @highAdminId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Users (Id, Username, PasswordHash, Email, PhoneNumber, FaceRegistered, MustChangePassword, TokenVersion, IsActive)
VALUES
    (@highAdminId, 'highadmin', '$2a$12$1z0WFrouH5JZdDkmpjQPiuyOcYIOeswMPhJMDa7VwJe9uT/d0QoD.', 'highadmin@mail.com', 0123456789, 0, 1, 1, 1);


INSERT INTO UserRoles (UserId, RoleId)
SELECT @highAdminId, Id FROM Roles WHERE Name = 'HighAdmin';

-----------------------------Thêm gg/ms authenticator----------------------------------
ALTER TABLE Users
    ADD TwoFactorEnabled BIT DEFAULT 0,
    TwoFactorSecret NVARCHAR(200) NULL;

-----------------------------Thêm User profile--------------------------------
-- Bảng UserProfiles
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

-- Bảng 1: Danh sách người theo dõi (Followers)
CREATE TABLE Followers (
                           UserId      UNIQUEIDENTIFIER NOT NULL,  -- Người được follow
                           FollowerId  UNIQUEIDENTIFIER NOT NULL,  -- Người theo dõi
                           FollowedAt  DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),

                           CONSTRAINT PK_Followers PRIMARY KEY (UserId, FollowerId),

    -- Khi người nổi tiếng bị xóa → xóa hết lượt follow vào họ
                           CONSTRAINT FK_Followers_UserId
                               FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,

    -- Khi một fan bị xóa → KHÔNG tự xóa ở đây (tránh multiple cascade)
                           CONSTRAINT FK_Followers_FollowerId
                               FOREIGN KEY (FollowerId) REFERENCES Users(Id) ON DELETE NO ACTION
);

-- Bảng 2: Danh sách người đang follow (Following)
CREATE TABLE Following (
                           UserId       UNIQUEIDENTIFIER NOT NULL,  -- Người đang follow
                           FollowingId  UNIQUEIDENTIFIER NOT NULL,  -- Người được follow
                           FollowedAt   DATETIME2(7)     NOT NULL DEFAULT SYSUTCDATETIME(),

                           CONSTRAINT PK_Following PRIMARY KEY (UserId, FollowingId),

    -- Khi user bị xóa → tự xóa hết các lượt user đó đang follow người khác
                           CONSTRAINT FK_Following_UserId
                               FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,

    -- Khi người được follow bị xóa → KHÔNG tự xóa ở đây
                           CONSTRAINT FK_Following_FollowingId
                               FOREIGN KEY (FollowingId) REFERENCES Users(Id) ON DELETE NO ACTION
);


-- Bảng Post
CREATE TABLE Posts (
                       Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                       UserId UNIQUEIDENTIFIER NOT NULL,
                       Title NVARCHAR(300) NOT NULL,
                       Content NVARCHAR(MAX) NULL,
                       CreatedAt DATETIME DEFAULT GETUTCDATE(),
                       UpdatedAt DATETIME NULL,
                       IsDeleted BIT NOT NULL DEFAULT 0,
                       IsVisible BIT NOT NULL DEFAULT 1,
                       VisibleToRoles NVARCHAR(50)
                        CHECK (VisibleToRoles IN ('Student', 'Teacher', 'Admin', 'All')),
                       FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

-- Bảng Post Comment
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

-- Bảng Post Vote
CREATE TABLE PostVotes (
                           PostId UNIQUEIDENTIFIER NOT NULL,
                           UserId UNIQUEIDENTIFIER NOT NULL,
                           VoteType BIT NOT NULL CHECK (VoteType IN (0,1)),
                           VotedAt DATETIME DEFAULT GETUTCDATE(),

                           PRIMARY KEY (PostId, UserId),

                           FOREIGN KEY (PostId) REFERENCES Posts(Id) ON DELETE CASCADE,
                           FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTIOn
);


CREATE INDEX IX_Posts_UserId ON Posts(UserId);

-- Bảng Post Report
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

                             FOREIGN KEY (PostId) REFERENCES Posts2(Id) ON DELETE CASCADE,
                             FOREIGN KEY (ReportedBy) REFERENCES Users(Id) ON DELETE NO ACTION,
                             FOREIGN KEY (ReviewedBy) REFERENCES Users(Id) ON DELETE NO ACTION
);

CREATE INDEX IX_PostReports_PostId ON PostReports(PostId);


-- Index để query nhanh
CREATE NONCLUSTERED INDEX IX_Followers_FollowerId   ON Followers(FollowerId);
CREATE NONCLUSTERED INDEX IX_Following_FollowingId ON Following(FollowingId);

CREATE PROCEDURE usp_DeleteUser @userId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
BEGIN TRAN;

    -- Dọn 2 phần còn sót do NO ACTION
DELETE FROM Followers WHERE FollowerId = @userId;   -- user này từng follow ai
DELETE FROM Following WHERE FollowingId = @userId;  -- ai đó follow user này

-- Bây giờ xóa user → 2 cascade kia sẽ tự chạy
DELETE FROM Users WHERE Id = @userId;

COMMIT TRAN;
END


ALTER TABLE UserRoles DROP CONSTRAINT FK__UserRoles__UserI__693CA210;
ALTER TABLE UserRoles
    ADD CONSTRAINT FK_UserRoles_UserId
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE;

ALTER TABLE OtpCodes DROP CONSTRAINT FK__OtpCodes__UserId__778AC167;
ALTER TABLE OtpCodes
    ADD CONSTRAINT FK_OtpCodes_UserId
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE;


-----------------------------------Thêm Recovery Code-----------------------------------
ALTER TABLE Users
    ADD
        RecoveryCodesGenerated BIT DEFAULT 0,        -- Đã từng tạo code chưa
    RecoveryCodesLeft       INT  DEFAULT 0;       -- Còn bao nhiêu code chưa dùng

CREATE TABLE RecoveryCodes (
                               Id         UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                               UserId     UNIQUEIDENTIFIER NOT NULL,
                               HashedCode NVARCHAR(255) NOT NULL,
                               IsUsed     BIT NOT NULL DEFAULT 0,
                               CreatedAt  DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
                               UsedAt     DATETIME2(7) NULL,

                               CONSTRAINT FK_RecoveryCodes_UserId
                                   FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE NONCLUSTERED INDEX IX_RecoveryCodes_UserId_IsUsed 
ON RecoveryCodes (UserId, IsUsed) 
INCLUDE (HashedCode);
    
