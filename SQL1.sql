CREATE table Users (
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
                            ExpiredAt DATETIME NOT NULL, -- Hết hạn sau 7 ngày
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
CREATE INDEX IX_TrustedDevices_UserId_IP_Device
    ON TrustedDevices (UserId, IPAddress, DeviceInfo);

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

--- highadmin, Admin123.
INSERT INTO UserRoles (UserId, RoleId)
SELECT @highAdminId, Id FROM Roles WHERE Name = 'HighAdmin';

------------------------------------Chat--------------------------------------------
-- 1. Conversations (đoạn chat 1-1 hoặc nhóm)
CREATE TABLE Conversations (
                               Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                               Type          TINYINT NOT NULL,           -- 0 = 1-1, 1 = Group
                               Name          NVARCHAR(100) NULL,         -- NULL nếu 1-1, chỉ bắt nhập khi chat nhóm
                               Avatar        NVARCHAR(500) NULL,
                               CreatedAt     DATETIME2 DEFAULT GETUTCDATE(),
                               CreatorId     UNIQUEIDENTIFIER NULL ,         -- FK Users(Id)
);

-- 2. ConversationMembers (ai trong đoạn chat nào)
CREATE TABLE ConversationMembers (
                                     ConversationId UNIQUEIDENTIFIER NOT NULL,
                                     UserId			UNIQUEIDENTIFIER NOT NULL,
                                     JoinedAt       DATETIME2 DEFAULT GETUTCDATE(),
                                     Role           TINYINT DEFAULT 0,          -- 0=member, 1=Admin(chỉ có nếu là nhóm)
                                     PRIMARY KEY (ConversationId, UserId),
                                     FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE,
                                     --FOREIGN KEY (UserId) REFERENCES Users(Id)   -- XEM xét nên xóa
);

-- 3. Messages (tin nhắn + attachment)
CREATE TABLE Messages (
                          Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
                          ConversationId UNIQUEIDENTIFIER NOT NULL,
                          SenderId      UNIQUEIDENTIFIER NOT NULL,
                          MessageType   TINYINT NOT NULL,           -- 0=text, 1=image, 2=video, 3=file
                          CipherText    NVARCHAR(MAX) NOT NULL,     -- nội dung hoặc URL đã E2EE
                          PinExchange   NVARCHAR(MAX) NULL,         -- RSA encrypted PIN (chỉ lần đầu hoặc đổi PIN)
                          ReplyToId     BIGINT NULL,                -- trích dẫn tin (trả lời)
                          CreatedAt     DATETIME2 DEFAULT GETUTCDATE(),

                          FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE,
                        --  FOREIGN KEY (SenderId) REFERENCES Users(Id),
                          FOREIGN KEY (ReplyToId) REFERENCES Messages(Id),

                          INDEX IX_Conv_Created (ConversationId, CreatedAt DESC),
                          INDEX IX_Sender_Created (SenderId, CreatedAt DESC)
);

-- 6. Message Attachments - Metadata file từ Cloudinary
CREATE TABLE MessageAttachments (
                                    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
                                    MessageId       BIGINT NOT NULL,
                                    CloudinaryUrl   NVARCHAR(1000) NOT NULL,
                                    ResourceType    NVARCHAR(20) NOT NULL,
                                    Format          NVARCHAR(20) NOT NULL,
                                    FileName        NVARCHAR(255) NULL,
                                    UploadedAt      DATETIME2 DEFAULT SYSUTCDATETIME(),

                                    CONSTRAINT FK_MessageAttachments_MessageId
                                        FOREIGN KEY (MessageId) REFERENCES Messages(Id) ON DELETE CASCADE
);



-- 6. MessageNotifications (Chấm đỏ + sắp xếp danh sách chat) – Chỉ dùng cho UI hiện thông báo, không ảnh hưởng đến code
CREATE TABLE MessageNotifications (
                                      RecipientId   UNIQUEIDENTIFIER NOT NULL,
                                      SenderId      UNIQUEIDENTIFIER NOT NULL,
                                      UnreadCount   INT NOT NULL DEFAULT 1,
                                      LastMessageId BIGINT NULL,
                                      LastSentAt    DATETIME2 DEFAULT GETUTCDATE(),
                                      PRIMARY KEY (RecipientId, SenderId),
                                      FOREIGN KEY (LastMessageId) REFERENCES Messages(Id),
                                      INDEX IX_Recipient_Unread (RecipientId, UnreadCount DESC)
);

-------------------------------------Mã Hóa--------------------------------------------------------

-- 1. Bảng ChatUsers – bắt buộc phải có để kích hoạt tính năng chat
-- Chỉ user có trong bảng này mới được tìm kiếm và chat
CREATE TABLE ChatUsers (
                           UserId		 UNIQUEIDENTIFIER PRIMARY KEY,
                           Username      NVARCHAR(256) NOT NULL,        -- trùng với Users.UserName
                           DisplayName   NVARCHAR(100) NOT NULL,        -- tên hiển thị trong chat
                           PinCodeHash   NVARCHAR(256) NOT NULL,        -- SHA-256 của PIN (client băm trước khi gửi)
                           CreatedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                           UpdatedAt     DATETIME2     NOT NULL DEFAULT GETUTCDATE(),

                           CONSTRAINT FK_ChatUsers_UserId
                               FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,

    -- Đảm bảo 1 user chỉ xuất hiện 1 lần
                           UNIQUE (Username)
);

-- Index để tìm kiếm nhanh theo DisplayName hoặc Username
CREATE INDEX IX_ChatUsers_Search ON ChatUsers (DisplayName);
CREATE INDEX IX_ChatUsers_Username ON ChatUsers (Username);

--------------------------------------------------------------------

-- 2. Bảng UserRsaKeys – đúng theo mô tả của bạn
-- Lưu PublicKey và PrivateKey đã được mã hóa AES bằng PIN (phía client)
CREATE TABLE UserRsaKeys (
                             UserId               UNIQUEIDENTIFIER PRIMARY KEY,
                             PublicKey            NVARCHAR(MAX) NOT NULL,        -- PEM format
                             PrivateKeyEncrypted  NVARCHAR(MAX) NOT NULL,        -- đã AES bằng PIN (client encrypt)
                             CreatedAt            DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
                             ExpiresAt            DATETIME2     NOT NULL,          -- 30 ngày
                             IsActive             BIT           NOT NULL DEFAULT 1,

                             CONSTRAINT FK_UserRsaKeys_UserId
                                 FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,

);
-- Filtered Unique Index để đảm bảo chỉ có 1 bản ghi active tại 1 thời điểm
CREATE UNIQUE INDEX IX_UserRsaKeys_Active_User
    ON UserRsaKeys (UserId)
    WHERE IsActive = 1;
-- Index để kiểm tra key hết hạn nhanh
CREATE INDEX IX_UserRsaKeys_ExpiresAt ON UserRsaKeys (ExpiresAt);
CREATE INDEX IX_UserRsaKeys_IsActive ON UserRsaKeys (IsActive);


CREATE TABLE ConversationKeys (
                                  UserId          UNIQUEIDENTIFIER NOT NULL,
                                  ConversationId  UNIQUEIDENTIFIER NOT NULL,

                                  KeyVersion      INT NOT NULL DEFAULT 1,

                                  EncryptedKey    NVARCHAR(MAX) NOT NULL,

                                  UpdatedAt       DATETIME2 DEFAULT GETUTCDATE(),

                                  CONSTRAINT PK_ConversationKeys PRIMARY KEY (UserId, ConversationId, KeyVersion),

                                  CONSTRAINT FK_ConversationKeys_ConversationId
                                      FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE
);