-- ===================================================
-- COMPLETE CHAT FEATURE DATABASE SCHEMA
-- SchoolBook Platform - Chat System
-- ===================================================

USE [YourDatabaseName]; -- Thay bằng tên database của bạn
GO

-- ===================================================
-- 1. ChatThreads Table
-- Lưu thông tin các cuộc hội thoại (thread/conversation)
-- ===================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatThreads]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[ChatThreads] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [ThreadName] NVARCHAR(200) NOT NULL,
    [UserIds] NVARCHAR(MAX) NOT NULL,  -- JSON array của user IDs

    CONSTRAINT [PK_ChatThreads] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

PRINT 'Created table: ChatThreads';
END
ELSE
BEGIN
    PRINT 'Table ChatThreads already exists';
END
GO

-- ===================================================
-- 2. ChatSegments Table
-- Lưu các đoạn chat (segment) trong mỗi thread
-- Mỗi segment có thể được bảo vệ bằng PIN
-- ===================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatSegments]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[ChatSegments] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [ThreadId] INT NOT NULL,
    [StartTime] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),
    [EndTime] DATETIME2(7) NULL,
    [MessagesJson] NVARCHAR(MAX) NOT NULL DEFAULT '[]',  -- JSON array của messages
    [IsProtected] BIT NOT NULL DEFAULT 0,
    [PinHash] NVARCHAR(MAX) NULL,  -- Hash của PIN (chỉ khi IsProtected = true)
    [Salt] VARBINARY(MAX) NULL,    -- Salt cho PIN hashing (chỉ khi IsProtected = true)

    CONSTRAINT [PK_ChatSegments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChatSegments_ChatThreads] FOREIGN KEY ([ThreadId])
    REFERENCES [dbo].[ChatThreads] ([Id])
    ON DELETE CASCADE
    );

-- Tạo index cho ThreadId để tăng tốc query
CREATE NONCLUSTERED INDEX [IX_ChatSegments_ThreadId] 
    ON [dbo].[ChatSegments] ([ThreadId]);
    
    PRINT 'Created table: ChatSegments with FK to ChatThreads';
END
ELSE
BEGIN
    PRINT 'Table ChatSegments already exists';
END
GO

-- ===================================================
-- 3. ChatAttachments Table
-- Lưu các file đính kèm trong chat (images, videos, documents)
-- ===================================================

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatAttachments]') AND type in (N'U'))
BEGIN
CREATE TABLE [dbo].[ChatAttachments] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [SegmentId] INT NOT NULL,
    [MessageIndex] INT NOT NULL,           -- Vị trí của message trong MessagesJson array
    [FileName] NVARCHAR(255) NOT NULL,
    [FileType] NVARCHAR(50) NOT NULL,      -- 'image', 'video', 'file'
    [MimeType] NVARCHAR(100) NOT NULL,     -- 'image/jpeg', 'video/mp4', etc.
    [FileSize] BIGINT NOT NULL,            -- Size in bytes
    [FileData] VARBINARY(MAX) NOT NULL,    -- Binary data của file
    [UploadedAt] DATETIME2(7) NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT [PK_ChatAttachments] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChatAttachments_ChatSegments] FOREIGN KEY ([SegmentId])
    REFERENCES [dbo].[ChatSegments] ([Id])
    ON DELETE CASCADE
    );

-- Tạo index cho SegmentId để tăng tốc query
CREATE NONCLUSTERED INDEX [IX_ChatAttachments_SegmentId] 
    ON [dbo].[ChatAttachments] ([SegmentId]);
    
    PRINT 'Created table: ChatAttachments with FK to ChatSegments';
END
ELSE
BEGIN
    PRINT 'Table ChatAttachments already exists';
END
GO

-- ===================================================
-- 4. Add Default Constraints (nếu chưa có)
-- ===================================================

-- Default constraint cho ChatSegments.MessagesJson
IF NOT EXISTS (
    SELECT * FROM sys.default_constraints 
    WHERE name = 'DF_ChatSegments_MessagesJson'
    AND parent_object_id = OBJECT_ID('ChatSegments')
)
BEGIN
ALTER TABLE [dbo].[ChatSegments]
    ADD CONSTRAINT [DF_ChatSegments_MessagesJson] DEFAULT '[]' FOR [MessagesJson];

PRINT 'Added default constraint: ChatSegments.MessagesJson';
END
GO

-- Default constraint cho ChatSegments.StartTime
IF NOT EXISTS (
    SELECT * FROM sys.default_constraints 
    WHERE name = 'DF_ChatSegments_StartTime'
    AND parent_object_id = OBJECT_ID('ChatSegments')
)
BEGIN
ALTER TABLE [dbo].[ChatSegments]
    ADD CONSTRAINT [DF_ChatSegments_StartTime] DEFAULT GETUTCDATE() FOR [StartTime];

PRINT 'Added default constraint: ChatSegments.StartTime';
END
GO

-- Default constraint cho ChatSegments.IsProtected
IF NOT EXISTS (
    SELECT * FROM sys.default_constraints 
    WHERE name = 'DF_ChatSegments_IsProtected'
    AND parent_object_id = OBJECT_ID('ChatSegments')
)
BEGIN
ALTER TABLE [dbo].[ChatSegments]
    ADD CONSTRAINT [DF_ChatSegments_IsProtected] DEFAULT 0 FOR [IsProtected];

PRINT 'Added default constraint: ChatSegments.IsProtected';
END
GO

-- Default constraint cho ChatAttachments.UploadedAt
IF NOT EXISTS (
    SELECT * FROM sys.default_constraints 
    WHERE name = 'DF_ChatAttachments_UploadedAt'
    AND parent_object_id = OBJECT_ID('ChatAttachments')
)
BEGIN
ALTER TABLE [dbo].[ChatAttachments]
    ADD CONSTRAINT [DF_ChatAttachments_UploadedAt] DEFAULT GETUTCDATE() FOR [UploadedAt];

PRINT 'Added default constraint: ChatAttachments.UploadedAt';
END
GO

-- ===================================================
-- 5. Verify Schema
-- ===================================================

PRINT '';
PRINT 'SCHEMA VERIFICATION:';
PRINT '========================';

-- Check ChatThreads
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatThreads]'))
    PRINT 'ChatThreads: EXISTS'
ELSE
    PRINT 'ChatThreads: NOT FOUND';

-- Check ChatSegments
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatSegments]'))
    PRINT 'ChatSegments: EXISTS'
ELSE
    PRINT 'ChatSegments: NOT FOUND';

-- Check ChatAttachments
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ChatAttachments]'))
    PRINT 'ChatAttachments: EXISTS'
ELSE
    PRINT 'ChatAttachments: NOT FOUND';

-- ===================================================
-- 6. Display Table Structures
-- ===================================================

PRINT '';
PRINT 'TABLE STRUCTURES:';
PRINT '====================';

-- ChatThreads structure
PRINT '';
PRINT 'ChatThreads:';
SELECT
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable,
    ISNULL(dc.definition, '') AS DefaultValue
FROM sys.columns c
         INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
         LEFT JOIN sys.default_constraints dc
                   ON dc.parent_object_id = c.object_id
                       AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID('ChatThreads')
ORDER BY c.column_id;

-- ChatSegments structure
PRINT '';
PRINT 'ChatSegments:';
SELECT
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable,
    ISNULL(dc.definition, '') AS DefaultValue
FROM sys.columns c
         INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
         LEFT JOIN sys.default_constraints dc
                   ON dc.parent_object_id = c.object_id
                       AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID('ChatSegments')
ORDER BY c.column_id;

-- ChatAttachments structure
PRINT '';
PRINT 'ChatAttachments:';
SELECT
    c.name AS ColumnName,
    t.name AS DataType,
    c.max_length AS MaxLength,
    c.is_nullable AS IsNullable,
    ISNULL(dc.definition, '') AS DefaultValue
FROM sys.columns c
         INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
         LEFT JOIN sys.default_constraints dc
                   ON dc.parent_object_id = c.object_id
                       AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID('ChatAttachments')
ORDER BY c.column_id;

-- ===================================================
-- 7. Display Foreign Keys
-- ===================================================

PRINT '';
PRINT '🔗 FOREIGN KEY RELATIONSHIPS:';
PRINT '==============================';

SELECT
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS TableName,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ColumnName,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn,
    fk.delete_referential_action_desc AS OnDelete
FROM sys.foreign_keys AS fk
         INNER JOIN sys.foreign_key_columns AS fkc
                    ON fk.object_id = fkc.constraint_object_id
WHERE fk.parent_object_id IN (
                              OBJECT_ID('ChatThreads'),
                              OBJECT_ID('ChatSegments'),
                              OBJECT_ID('ChatAttachments')
    )
ORDER BY TableName, ForeignKeyName;

-- ===================================================
-- 8. Display Indexes
-- ===================================================

PRINT '';
PRINT 'INDEXES:';
PRINT '===========';

SELECT
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    COL_NAME(ic.object_id, ic.column_id) AS ColumnName,
    i.is_unique AS IsUnique
FROM sys.indexes i
         INNER JOIN sys.index_columns ic
                    ON i.object_id = ic.object_id
                        AND i.index_id = ic.index_id
WHERE i.object_id IN (
                      OBJECT_ID('ChatThreads'),
                      OBJECT_ID('ChatSegments'),
                      OBJECT_ID('ChatAttachments')
    )
  AND i.is_primary_key = 0
ORDER BY TableName, IndexName;

PRINT '';
PRINT 'CHAT DATABASE SCHEMA SETUP COMPLETED!';
PRINT '=========================================';
GO

-- Create UserEncryptionKeys table
CREATE TABLE [dbo].[UserEncryptionKeys] (
    [Id] INT IDENTITY(1,1) NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [PublicKey] NVARCHAR(4000) NOT NULL,
    [EncryptedPrivateKey] NVARCHAR(4000) NULL,
    [PrivateKeySalt] VARBINARY(MAX) NULL,
    [CreatedAt] DATETIME2 NOT NULL DEFAULT (GETUTCDATE()),
    [LastUsedAt] DATETIME2 NULL,

    CONSTRAINT [PK_UserEncryptionKeys] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserEncryptionKeys_Users_UserId]
    FOREIGN KEY ([UserId]) REFERENCES [Users]([Id]) ON DELETE CASCADE
    );

-- Create unique index
CREATE UNIQUE INDEX [IX_UserEncryptionKeys_UserId]
    ON [UserEncryptionKeys]([UserId]);

PRINT '✅ UserEncryptionKeys table created successfully';