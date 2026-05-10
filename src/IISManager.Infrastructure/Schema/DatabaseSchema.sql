-- IIS Manager Platform - Database Schema
-- Idempotent: safe to run multiple times (uses IF NOT EXISTS / IF OBJECT_ID checks)
-- Target: SQL Server 2019+

USE IISManagerDb;
GO

-- ============================================================
-- Servers
-- ============================================================
IF OBJECT_ID('Servers', 'U') IS NULL
CREATE TABLE Servers (
    Id              INT             IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(100)   NOT NULL,
    Hostname        NVARCHAR(255)   NOT NULL,
    IpAddress       NVARCHAR(45)    NOT NULL,
    Environment     NVARCHAR(50)    NOT NULL,         -- Development, Staging, UAT, Production
    [Group]         NVARCHAR(100)   NULL,
    Description     NVARCHAR(500)   NULL,
    AgentApiKey     NVARCHAR(64)    NOT NULL,         -- SHA256 hash of the plain-text key
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Unknown',
    AgentConnectionId NVARCHAR(128) NULL,
    LastHeartbeat   DATETIME2       NULL,
    AgentVersion    NVARCHAR(20)    NULL,
    IsActive        BIT             NOT NULL DEFAULT 1,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       NVARCHAR(150)   NOT NULL
);
GO

-- ============================================================
-- Applications
-- ============================================================
IF OBJECT_ID('Applications', 'U') IS NULL
CREATE TABLE Applications (
    Id                  INT             IDENTITY(1,1) PRIMARY KEY,
    Name                NVARCHAR(100)   NOT NULL,
    Description         NVARCHAR(500)   NULL,
    DefaultServerId     INT             NULL REFERENCES Servers(Id),
    DefaultWebsiteName  NVARCHAR(200)   NOT NULL DEFAULT '',
    DefaultAppPoolName  NVARCHAR(200)   NOT NULL DEFAULT '',
    PhysicalPath        NVARCHAR(500)   NOT NULL DEFAULT '',
    IsActive            BIT             NOT NULL DEFAULT 1,
    CreatedAt           DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy           NVARCHAR(150)   NOT NULL
);
GO

-- ============================================================
-- DeploymentPackages
-- ============================================================
IF OBJECT_ID('DeploymentPackages', 'U') IS NULL
CREATE TABLE DeploymentPackages (
    Id              INT             IDENTITY(1,1) PRIMARY KEY,
    FileName        NVARCHAR(255)   NOT NULL,
    StoredPath      NVARCHAR(1000)  NOT NULL,
    Sha256Hash      NVARCHAR(64)    NOT NULL,
    SizeBytes       BIGINT          NOT NULL,
    Version         NVARCHAR(50)    NOT NULL,
    ApplicationId   INT             NOT NULL REFERENCES Applications(Id),
    UploadedAt      DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    UploadedBy      NVARCHAR(150)   NOT NULL,
    IsDeleted       BIT             NOT NULL DEFAULT 0
);
GO

-- ============================================================
-- Deployments
-- ============================================================
IF OBJECT_ID('Deployments', 'U') IS NULL
CREATE TABLE Deployments (
    Id                          INT             IDENTITY(1,1) PRIMARY KEY,
    CorrelationId               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    ApplicationId               INT             NOT NULL REFERENCES Applications(Id),
    PackageId                   INT             NOT NULL REFERENCES DeploymentPackages(Id),
    Version                     NVARCHAR(50)    NOT NULL,
    Status                      NVARCHAR(20)    NOT NULL DEFAULT 'Queued',
    Mode                        NVARCHAR(20)    NOT NULL DEFAULT 'Sequential',
    DeployedBy                  NVARCHAR(150)   NOT NULL,
    CreatedAt                   DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    StartedAt                   DATETIME2       NULL,
    CompletedAt                 DATETIME2       NULL,
    Notes                       NVARCHAR(MAX)   NULL,
    IsRollback                  BIT             NOT NULL DEFAULT 0,
    RollbackTargetDeploymentId  INT             NULL REFERENCES Deployments(Id),
    FailureReason               NVARCHAR(MAX)   NULL
);
GO
CREATE NONCLUSTERED INDEX IX_Deployments_ApplicationId ON Deployments(ApplicationId);
CREATE NONCLUSTERED INDEX IX_Deployments_Status ON Deployments(Status);
CREATE NONCLUSTERED INDEX IX_Deployments_CreatedAt ON Deployments(CreatedAt DESC);
GO

-- ============================================================
-- DeploymentTargets
-- ============================================================
IF OBJECT_ID('DeploymentTargets', 'U') IS NULL
CREATE TABLE DeploymentTargets (
    Id              INT             IDENTITY(1,1) PRIMARY KEY,
    DeploymentId    INT             NOT NULL REFERENCES Deployments(Id),
    ServerId        INT             NOT NULL REFERENCES Servers(Id),
    WebsiteName     NVARCHAR(200)   NOT NULL,
    AppPoolName     NVARCHAR(200)   NOT NULL,
    PhysicalPath    NVARCHAR(500)   NOT NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Queued',
    StartedAt       DATETIME2       NULL,
    CompletedAt     DATETIME2       NULL,
    FailureReason   NVARCHAR(MAX)   NULL
);
GO
CREATE NONCLUSTERED INDEX IX_DeploymentTargets_DeploymentId ON DeploymentTargets(DeploymentId);
GO

-- ============================================================
-- DeploymentLogs
-- ============================================================
IF OBJECT_ID('DeploymentLogs', 'U') IS NULL
CREATE TABLE DeploymentLogs (
    Id              BIGINT          IDENTITY(1,1) PRIMARY KEY,
    DeploymentId    INT             NOT NULL REFERENCES Deployments(Id),
    ServerId        INT             NULL REFERENCES Servers(Id),
    Message         NVARCHAR(MAX)   NOT NULL,
    Level           NVARCHAR(20)    NOT NULL DEFAULT 'Info',
    Timestamp       DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO
CREATE NONCLUSTERED INDEX IX_DeploymentLogs_DeploymentId ON DeploymentLogs(DeploymentId, Timestamp);
GO

-- ============================================================
-- Websites (IIS state cache)
-- ============================================================
IF OBJECT_ID('Websites', 'U') IS NULL
CREATE TABLE Websites (
    Id              INT             IDENTITY(1,1) PRIMARY KEY,
    ServerId        INT             NOT NULL REFERENCES Servers(Id),
    IISId           BIGINT          NOT NULL,
    Name            NVARCHAR(200)   NOT NULL,
    PhysicalPath    NVARCHAR(500)   NOT NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Unknown',
    DefaultAppPool  NVARCHAR(200)   NOT NULL DEFAULT '',
    BindingsJson    NVARCHAR(MAX)   NOT NULL DEFAULT '[]',
    LastSyncedAt    DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_Websites_ServerId_IISId UNIQUE (ServerId, IISId)
);
GO

-- ============================================================
-- ApplicationPools (IIS state cache)
-- ============================================================
IF OBJECT_ID('ApplicationPools', 'U') IS NULL
CREATE TABLE ApplicationPools (
    Id              INT             IDENTITY(1,1) PRIMARY KEY,
    ServerId        INT             NOT NULL REFERENCES Servers(Id),
    Name            NVARCHAR(200)   NOT NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Unknown',
    RuntimeVersion  NVARCHAR(20)    NOT NULL DEFAULT 'v4.0',
    PipelineMode    NVARCHAR(20)    NOT NULL DEFAULT 'Integrated',
    AutoStart       BIT             NOT NULL DEFAULT 1,
    LastSyncedAt    DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_AppPools_ServerId_Name UNIQUE (ServerId, Name)
);
GO

-- ============================================================
-- ServerHealth (time-series)
-- ============================================================
IF OBJECT_ID('ServerHealth', 'U') IS NULL
CREATE TABLE ServerHealth (
    Id              BIGINT          IDENTITY(1,1) PRIMARY KEY,
    ServerId        INT             NOT NULL REFERENCES Servers(Id),
    CpuPercent      DECIMAL(5,2)    NOT NULL,
    RamUsedMB       BIGINT          NOT NULL,
    RamTotalMB      BIGINT          NOT NULL,
    DiskUsedGB      BIGINT          NOT NULL,
    DiskTotalGB     BIGINT          NOT NULL,
    IISRunning      BIT             NOT NULL,
    RunningSites    INT             NOT NULL DEFAULT 0,
    RunningAppPools INT             NOT NULL DEFAULT 0,
    AgentVersion    NVARCHAR(20)    NULL,
    RecordedAt      DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO
CREATE NONCLUSTERED INDEX IX_ServerHealth_ServerId_RecordedAt ON ServerHealth(ServerId, RecordedAt DESC);
GO

-- ============================================================
-- AuditLogs
-- ============================================================
IF OBJECT_ID('AuditLogs', 'U') IS NULL
CREATE TABLE AuditLogs (
    Id              BIGINT          IDENTITY(1,1) PRIMARY KEY,
    Action          NVARCHAR(50)    NOT NULL,
    EntityType      NVARCHAR(50)    NOT NULL,
    EntityId        NVARCHAR(50)    NULL,
    Description     NVARCHAR(500)   NULL,
    OldValues       NVARCHAR(MAX)   NULL,
    NewValues       NVARCHAR(MAX)   NULL,
    PerformedBy     NVARCHAR(150)   NOT NULL,
    IpAddress       NVARCHAR(45)    NULL,
    PerformedAt     DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO
CREATE NONCLUSTERED INDEX IX_AuditLogs_PerformedAt ON AuditLogs(PerformedAt DESC);
CREATE NONCLUSTERED INDEX IX_AuditLogs_PerformedBy ON AuditLogs(PerformedBy);
GO

-- ============================================================
-- Notifications
-- ============================================================
IF OBJECT_ID('Notifications', 'U') IS NULL
CREATE TABLE Notifications (
    Id              BIGINT          IDENTITY(1,1) PRIMARY KEY,
    Title           NVARCHAR(200)   NOT NULL,
    Message         NVARCHAR(MAX)   NOT NULL,
    Type            NVARCHAR(20)    NOT NULL DEFAULT 'Info',
    ActionUrl       NVARCHAR(500)   NULL,
    TargetUser      NVARCHAR(150)   NULL,   -- NULL = broadcast to all
    IsRead          BIT             NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO
CREATE NONCLUSTERED INDEX IX_Notifications_IsRead_CreatedAt ON Notifications(IsRead, CreatedAt DESC);
GO

-- ============================================================
-- Create initial database (run separately if needed)
-- ============================================================
-- CREATE DATABASE IISManagerDb COLLATE SQL_Latin1_General_CP1_CI_AS;
GO
