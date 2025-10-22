CREATE TABLE Users (
    UserId INT IDENTITY PRIMARY KEY,
    Username NVARCHAR(50) UNIQUE NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    Role NVARCHAR(20) NOT NULL CHECK (Role IN ('HighAdmin','Admin','Moder','Teacher','Student')),
    TokenVersion INT DEFAULT 1,
    Phone NVARCHAR(20),
    Email NVARCHAR(100),
    FaceRegistered BIT DEFAULT 0,
    FaceId NVARCHAR(100) NULL,
    OtpPreference NVARCHAR(10) DEFAULT 'email', -- 'sms' or 'email'
    CreatedAt DATETIME DEFAULT GETDATE()
);

CREATE TABLE UserTokens (
    TokenId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId INT FOREIGN KEY REFERENCES Users(UserId),
    DeviceInfo NVARCHAR(200),
    IPAddress NVARCHAR(50),
    CreatedAt DATETIME DEFAULT GETDATE(),
    ExpiredAt DATETIME,
    IsRevoked BIT DEFAULT 0
);

CREATE TABLE OtpLogs (
    Id INT IDENTITY PRIMARY KEY,
    UserId INT FOREIGN KEY REFERENCES Users(UserId),
    Code NVARCHAR(6),
    ExpiredAt DATETIME,
    IsUsed BIT DEFAULT 0
);

INSERT INTO Users (Username, PasswordHash, Role)
VALUES ('admin', '$2a$12$YrgQ2QfLKxRUE65jCq5zM.Jcw5q2rJBqLMil0PIS7rt/d7PgDfE.6', 'HighAdmin');

