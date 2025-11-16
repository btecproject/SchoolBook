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
    (@highAdminId, 'highadmin', '$2a$12$1z0WFrouH5JZdDkmpjQPiuyOcYIOeswMPhJMDa7VwJe9uT/d0QoD.', 'highadmin@mail.com', NULL, 0, 1, 1, 1);


INSERT INTO UserRoles (UserId, RoleId)
SELECT @highAdminId, Id FROM Roles WHERE Name = 'HighAdmin';

-- Chat
USE SchoolBookDB;
GO

-- Kiểm tra và tạo bảng ChatThreads
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatThreads')
BEGIN
CREATE TABLE ChatThreads (
                             Id INT PRIMARY KEY IDENTITY(1,1),
                             ThreadName NVARCHAR(200) NOT NULL,
                             UserIds NVARCHAR(MAX) NOT NULL
);
PRINT 'Created ChatThreads table';
END
ELSE
BEGIN
    PRINT 'ChatThreads table already exists';
END
GO

-- Kiểm tra và tạo bảng ChatSegments
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
PRINT 'Created ChatSegments table';
END
ELSE
BEGIN
    PRINT 'ChatSegments table already exists';
END
GO

-- Thêm dữ liệu mẫu cho ChatThreads
IF NOT EXISTS (SELECT * FROM ChatThreads)
BEGIN
INSERT INTO ChatThreads (ThreadName, UserIds) VALUES
                                                  (N'General Discussion', '["hungnp","user1","user2"]'),
                                                  (N'Project Team', '["hungnp","user1"]'),
                                                  (N'Study Group', '["user1","user2","user3"]'),
                                                  (N'Admin Chat', '["hungnp"]');
PRINT 'Seeded sample chat threads';
END
GO

-- Kiểm tra kết quả
SELECT 'ChatThreads' as TableName, COUNT() as RecordCount FROM ChatThreads
UNION ALL
SELECT 'ChatSegments', COUNT() FROM ChatSegments;

SELECT * FROM ChatThreads;
GO

--ChatAttachments moi
CREATE TABLE ChatAttachments (
                                 Id INT PRIMARY KEY IDENTITY(1,1),
                                 SegmentId INT NOT NULL,
                                 MessageIndex INT NOT NULL, 0
                                 FileName NVARCHAR(255) NOT NULL,
    FileType NVARCHAR(50) NOT NULL, 0
                                 MimeType NVARCHAR(100) NOT NULL,
    FileSize BIGINT NOT NULL,
    FileData VARBINARY(MAX) NOT NULL, 0
                                 UploadedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_ChatAttachments_Segments FOREIGN KEY (SegmentId) REFERENCES ChatSegments(Id) ON DELETE CASCADE
    );

CREATE INDEX IX_ChatAttachments_SegmentId ON ChatAttachments(SegmentId);


-- neu chat ko dc thi don du lieu trong db
UPDATE ChatSegments
SET MessagesJson = '[]'
WHERE MessagesJson IS NULL;

UPDATE ChatSegments
SET Salt = 0x00
WHERE Salt IS NULL;

UPDATE ChatSegments
SET PinHash = ''
WHERE PinHash IS NULL;