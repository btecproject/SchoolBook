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
-- 4. CREATE TABLES (CHAT SYSTEM - FIXED)
-- =======================================================

-- Bảng ChatUsers: Định danh người dùng trong hệ thống chat
CREATE TABLE ChatUsers (
                           Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(), -- ChatUserId
                           UserId UNIQUEIDENTIFIER NOT NULL,
                           Username NVARCHAR(256) NOT NULL,
                           DisplayName NVARCHAR(100) NOT NULL,
                           PinCodeHash NVARCHAR(256) NOT NULL,
                           IsActive BIT DEFAULT 1,
                           CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                           UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

                           CONSTRAINT FK_ChatUsers_UserId FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
    --CONSTRAINT UQ_ChatUsers_UserId UNIQUE (UserId),
    --CONSTRAINT UQ_ChatUsers_Username UNIQUE (Username)
);
CREATE UNIQUE INDEX IX_ChatUsers_Active ON ChatUsers(UserId) WHERE IsActive = 1;
CREATE UNIQUE INDEX IX_ChatUsers_Username_Active ON ChatUsers(Username) WHERE IsActive = 1;

CREATE INDEX IX_ChatUsers_Search ON ChatUsers (DisplayName);

-- Bảng UserRsaKeys: Lưu khóa E2EE của từng ChatUser
CREATE TABLE UserRsaKeys (
                             Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                             ChatUserId UNIQUEIDENTIFIER NOT NULL, -- Trỏ về ChatUsers
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
                               Type TINYINT NOT NULL, -- 0=Private, 1=Group
                               Name NVARCHAR(100) NULL,
                               Avatar NVARCHAR(500) NULL,
                               CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
                               CreatorId UNIQUEIDENTIFIER NULL,
                               FOREIGN KEY (CreatorId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE TABLE ConversationMembers (
                                     ConversationId UNIQUEIDENTIFIER NOT NULL,
                                     ChatUserId UNIQUEIDENTIFIER NOT NULL,
                                     JoinedAt DATETIME2 DEFAULT GETUTCDATE(),
                                     Role TINYINT DEFAULT 0, -- 0=Member, 1=Admin

                                     CONSTRAINT PK_ConversationMembers PRIMARY KEY (ConversationId, ChatUserId),
                                     FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE,
                                     FOREIGN KEY (ChatUserId) REFERENCES ChatUsers(Id) ON DELETE CASCADE
);

-- Bảng ConversationKeys: Lưu khóa AES của cuộc hội thoại (được mã hóa bằng Public Key của user)
CREATE TABLE ConversationKeys (
                                  ChatUserId UNIQUEIDENTIFIER NOT NULL,
                                  ConversationId UNIQUEIDENTIFIER NOT NULL,
                                  KeyVersion INT NOT NULL DEFAULT 1,
                                  EncryptedKey NVARCHAR(MAX) NOT NULL,
                                  UpdatedAt DATETIME2 DEFAULT GETUTCDATE(),

                                  CONSTRAINT PK_ConversationKeys PRIMARY KEY (ChatUserId, ConversationId, KeyVersion),
                                  FOREIGN KEY (ChatUserId) REFERENCES ChatUsers(Id) ON DELETE CASCADE, -- Cascade xóa user thì xóa key
                                  FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE
);

-- Bảng Messages
CREATE TABLE Messages (
                          Id BIGINT IDENTITY(1,1) PRIMARY KEY,
                          ConversationId UNIQUEIDENTIFIER NOT NULL,
                          SenderId UNIQUEIDENTIFIER NOT NULL, -- ChatUserId người gửi
                          MessageType TINYINT NOT NULL,       -- 0=text, 1=image, 2=video, 3=file
                          CipherText NVARCHAR(MAX) NOT NULL,
                          ReplyToId BIGINT NULL,
                          CreatedAt DATETIME2 DEFAULT GETUTCDATE(),

                          FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE,
                          FOREIGN KEY (SenderId) REFERENCES ChatUsers(Id), -- Có thể để No Action để giữ tin nhắn
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
-- 5. PROCEDURES & SEED DATA
-- =======================================================

CREATE PROCEDURE usp_DeleteUser @userId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
BEGIN TRAN;
DELETE FROM Followers WHERE FollowerId = @userId;
DELETE FROM Following WHERE FollowingId = @userId;
DELETE FROM Users WHERE Id = @userId;
COMMIT TRAN;
END
GO

INSERT INTO Roles (Name, Description) VALUES
('HighAdmin', 'Super admin'), ('Admin', 'System Admin'), ('Moderator', 'Mod'), ('Teacher', 'Teacher'), ('Student', 'Student');

DECLARE @highAdminId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Users (Id, Username, PasswordHash, Email, PhoneNumber, IsActive)
VALUES (@highAdminId, 'highadmin', '$2a$12$1z0WFrouH5JZdDkmpjQPiuyOcYIOeswMPhJMDa7VwJe9uT/d0QoD.', 'highadmin@mail.com', '0123456789', 1);

INSERT INTO UserRoles (UserId, RoleId) SELECT @highAdminId, Id FROM Roles WHERE Name = 'HighAdmin';
GO