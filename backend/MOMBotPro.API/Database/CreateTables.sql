-- ============================================================
-- MOMBot Pro — Database Schema
-- Run against: MOMBotProDB
-- ============================================================

USE MOMBotProDB;
GO

-- ── Users ──────────────────────────────────────────────────
CREATE TABLE Users (
    Id               UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY,
    FullName         NVARCHAR(150)   NOT NULL,
    Email            NVARCHAR(256)   NOT NULL UNIQUE,
    PasswordHash     NVARCHAR(256)   NOT NULL,
    CompanyName      NVARCHAR(200),
    Domain           NVARCHAR(100),
    Role             NVARCHAR(20)    NOT NULL DEFAULT 'User',  -- Admin | User
    SubscriptionPlan NVARCHAR(50)    NOT NULL DEFAULT 'free_trial',
    FreeTrialMeetingsLeft INT        NOT NULL DEFAULT 3,
    IsActive         BIT             NOT NULL DEFAULT 1,
    CreatedAt        DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt        DATETIME2       NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ── Subscriptions ──────────────────────────────────────────
CREATE TABLE Subscriptions (
    Id        UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY,
    UserId    UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Plan      NVARCHAR(50)     NOT NULL,
    StartDate DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    EndDate   DATETIME2,
    PriceUSD  DECIMAL(10,2),
    PriceINR  DECIMAL(10,2),
    Status    NVARCHAR(20)     NOT NULL DEFAULT 'Active',   -- Active | Expired | Cancelled
    CreatedAt DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ── Pipelines ─────────────────────────────────────────────
CREATE TABLE Pipelines (
    Id              UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY,
    UserId          UNIQUEIDENTIFIER REFERENCES Users(Id) ON DELETE SET NULL,
    ClientName      NVARCHAR(200)    NOT NULL,
    Status          NVARCHAR(20)     NOT NULL DEFAULT 'Pending',
    Transcript      NVARCHAR(MAX),
    BugSummary      NVARCHAR(MAX),
    MOMSummary      NVARCHAR(MAX),
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt     DATETIME2,
    DurationMinutes INT
);
GO

-- ── PipelineSteps ─────────────────────────────────────────
CREATE TABLE PipelineSteps (
    Id          UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY,
    PipelineId  UNIQUEIDENTIFIER NOT NULL REFERENCES Pipelines(Id) ON DELETE CASCADE,
    StepName    NVARCHAR(100)    NOT NULL,
    Status      NVARCHAR(20)     NOT NULL DEFAULT 'Waiting',
    Message     NVARCHAR(MAX),
    CompletedAt DATETIME2
);
GO

-- ── JiraTickets ───────────────────────────────────────────
CREATE TABLE JiraTickets (
    Id          UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY,
    PipelineId  UNIQUEIDENTIFIER NOT NULL REFERENCES Pipelines(Id) ON DELETE CASCADE,
    TicketKey   NVARCHAR(50)     NOT NULL,
    Summary     NVARCHAR(500),
    Description NVARCHAR(MAX),
    Priority    NVARCHAR(20)     NOT NULL DEFAULT 'Medium',
    Status      NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    CreatedAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ── BugAnalyses ───────────────────────────────────────────
CREATE TABLE BugAnalyses (
    Id             UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY,
    PipelineId     UNIQUEIDENTIFIER NOT NULL REFERENCES Pipelines(Id) ON DELETE CASCADE,
    FileName       NVARCHAR(500),
    LineNumber     INT,
    BugDescription NVARCHAR(MAX),
    RootCause      NVARCHAR(MAX),
    OriginalCode   NVARCHAR(MAX),
    FixedCode      NVARCHAR(MAX),
    SuggestedFix   NVARCHAR(MAX)
);
GO

-- ── GitHubResults ─────────────────────────────────────────
CREATE TABLE GitHubResults (
    Id          UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY,
    PipelineId  UNIQUEIDENTIFIER NOT NULL REFERENCES Pipelines(Id) ON DELETE CASCADE,
    BranchName  NVARCHAR(200),
    PRTitle     NVARCHAR(500),
    PRDescription NVARCHAR(MAX),
    PRUrl       NVARCHAR(500),
    PRNumber    NVARCHAR(20),
    Status      NVARCHAR(20)     NOT NULL DEFAULT 'Open'
);
GO

-- ── ZoomSessions ──────────────────────────────────────────
CREATE TABLE ZoomSessions (
    Id            UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY,
    UserId        UNIQUEIDENTIFIER REFERENCES Users(Id) ON DELETE SET NULL,
    BotId         NVARCHAR(200)    NOT NULL,
    MeetingUrl    NVARCHAR(500)    NOT NULL,
    ClientName    NVARCHAR(200)    NOT NULL,
    Status        NVARCHAR(20)     NOT NULL DEFAULT 'Joining',
    StatusMessage NVARCHAR(500),
    PipelineId    UNIQUEIDENTIFIER REFERENCES Pipelines(Id) ON DELETE SET NULL,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
GO

-- ── Integrations ──────────────────────────────────────────
CREATE TABLE Integrations (
    Id          UNIQUEIDENTIFIER DEFAULT NEWID() PRIMARY KEY,
    UserId      UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Type        NVARCHAR(50)     NOT NULL,  -- Jira | GitHub | Slack | Gmail
    ConfigJson  NVARCHAR(MAX),              -- encrypted JSON
    IsConnected BIT              NOT NULL DEFAULT 0,
    ConnectedAt DATETIME2
);
GO

-- Unique: one integration per type per user
CREATE UNIQUE INDEX UX_Integrations_UserId_Type ON Integrations(UserId, Type);
GO
