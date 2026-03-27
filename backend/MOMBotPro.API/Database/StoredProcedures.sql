-- ============================================================
-- MOMBot Pro — Stored Procedures
-- Run AFTER EF migrations have created the tables.
-- ============================================================

USE MOMBotProDB;
GO

-- ── sp_CreateUser ─────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_CreateUser
    @Id           UNIQUEIDENTIFIER,
    @FullName     NVARCHAR(200),
    @Email        NVARCHAR(200),
    @PasswordHash NVARCHAR(500),
    @CompanyName  NVARCHAR(200),
    @Domain       NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Users (Id, FullName, Email, PasswordHash, CompanyName, Domain,
                       Role, SubscriptionPlan, FreeTrialMeetingsLeft, IsActive, CreatedAt, UpdatedAt)
    VALUES (@Id, @FullName, @Email, @PasswordHash, @CompanyName, @Domain,
            'User', 'free_trial', 3, 1, GETUTCDATE(), GETUTCDATE());
    SELECT * FROM Users WHERE Id = @Id;
END
GO

-- ── sp_GetUserByEmail ─────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_GetUserByEmail
    @Email NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Users WHERE Email = @Email AND IsActive = 1;
END
GO

-- ── sp_GetUserById ────────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_GetUserById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Users WHERE Id = @Id AND IsActive = 1;
END
GO

-- ── sp_DecrementFreeTrial ─────────────────────────────────
CREATE OR ALTER PROCEDURE sp_DecrementFreeTrial
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Users
    SET FreeTrialMeetingsLeft = FreeTrialMeetingsLeft - 1,
        UpdatedAt             = GETUTCDATE()
    WHERE Id = @UserId
      AND FreeTrialMeetingsLeft > 0
      AND SubscriptionPlan = 'free_trial';
END
GO

-- ── sp_UpdateSubscription ─────────────────────────────────
CREATE OR ALTER PROCEDURE sp_UpdateSubscription
    @UserId   UNIQUEIDENTIFIER,
    @Plan     NVARCHAR(50),
    @PriceUSD DECIMAL(10,2),
    @PriceINR DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Users
    SET SubscriptionPlan      = @Plan,
        FreeTrialMeetingsLeft = CASE WHEN @Plan = 'free_trial' THEN 3 ELSE 9999 END,
        UpdatedAt             = GETUTCDATE()
    WHERE Id = @UserId;

    INSERT INTO Subscriptions (Id, UserId, Plan, StartDate, PriceUSD, PriceINR, Status, CreatedAt)
    VALUES (NEWID(), @UserId, @Plan, GETUTCDATE(), @PriceUSD, @PriceINR, 'Active', GETUTCDATE());
END
GO

-- ── sp_CreatePipeline ─────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_CreatePipeline
    @Id         UNIQUEIDENTIFIER,
    @UserId     UNIQUEIDENTIFIER = NULL,
    @ClientName NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO Pipelines (Id, UserId, ClientName, Status, CreatedAt)
    VALUES (@Id, @UserId, @ClientName, 'Pending', GETUTCDATE());

    INSERT INTO PipelineSteps (Id, PipelineId, StepName, Status) VALUES
        (NEWID(), @Id, 'Transcribe Audio',         'Waiting'),
        (NEWID(), @Id, 'Extract Bug from MOM',     'Waiting'),
        (NEWID(), @Id, 'Create Jira Ticket',       'Waiting'),
        (NEWID(), @Id, 'Scan Codebase',            'Waiting'),
        (NEWID(), @Id, 'Generate Fix',             'Waiting'),
        (NEWID(), @Id, 'Create Branch & Raise PR', 'Waiting');

    SELECT * FROM Pipelines WHERE Id = @Id;
END
GO

-- ── sp_UpdatePipelineStatus ───────────────────────────────
CREATE OR ALTER PROCEDURE sp_UpdatePipelineStatus
    @Id         UNIQUEIDENTIFIER,
    @Status     NVARCHAR(50),
    @MOMSummary NVARCHAR(MAX) = NULL,
    @BugSummary NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Pipelines
    SET Status      = @Status,
        MOMSummary  = ISNULL(@MOMSummary, MOMSummary),
        BugSummary  = ISNULL(@BugSummary, BugSummary),
        CompletedAt = CASE WHEN @Status IN ('Done','Failed') THEN GETUTCDATE() ELSE CompletedAt END
    WHERE Id = @Id;
END
GO

-- ── sp_GetPipelinesByUser ─────────────────────────────────
CREATE OR ALTER PROCEDURE sp_GetPipelinesByUser
    @UserId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT p.*, u.FullName, u.CompanyName
    FROM Pipelines p
    LEFT JOIN Users u ON p.UserId = u.Id
    WHERE (@UserId IS NULL OR p.UserId = @UserId)
    ORDER BY p.CreatedAt DESC;
END
GO

-- ── sp_GetAnalyticsByPeriod ───────────────────────────────
CREATE OR ALTER PROCEDURE sp_GetAnalyticsByPeriod
    @UserId UNIQUEIDENTIFIER = NULL,
    @Days   INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @FromDate DATETIME2 = DATEADD(DAY, -@Days, GETUTCDATE());
    SELECT
        COUNT(*)                                                    AS TotalCalls,
        SUM(CASE WHEN Status = 'Done'        THEN 1 ELSE 0 END)   AS DoneCount,
        SUM(CASE WHEN Status = 'Failed'      THEN 1 ELSE 0 END)   AS FailedCount,
        SUM(CASE WHEN MOMSummary IS NOT NULL THEN 1 ELSE 0 END)   AS MOMsGenerated,
        AVG(CAST(ISNULL(DurationMinutes, 0) AS FLOAT))            AS AvgDurationMinutes
    FROM Pipelines
    WHERE CreatedAt >= @FromDate
      AND (@UserId IS NULL OR UserId = @UserId);
END
GO

-- ── sp_SaveIntegration ────────────────────────────────────
CREATE OR ALTER PROCEDURE sp_SaveIntegration
    @UserId      UNIQUEIDENTIFIER,
    @Type        NVARCHAR(50),
    @ConfigJson  NVARCHAR(MAX),
    @IsConnected BIT
AS
BEGIN
    SET NOCOUNT ON;
    MERGE Integrations AS target
    USING (SELECT @UserId AS UserId, @Type AS Type) AS source
        ON target.UserId = source.UserId AND target.Type = source.Type
    WHEN MATCHED THEN
        UPDATE SET
            ConfigJson  = @ConfigJson,
            IsConnected = @IsConnected,
            ConnectedAt = CASE WHEN @IsConnected = 1 THEN GETUTCDATE() ELSE NULL END
    WHEN NOT MATCHED THEN
        INSERT (Id, UserId, Type, ConfigJson, IsConnected, ConnectedAt)
        VALUES (NEWID(), @UserId, @Type, @ConfigJson, @IsConnected,
                CASE WHEN @IsConnected = 1 THEN GETUTCDATE() ELSE NULL END);

    SELECT * FROM Integrations WHERE UserId = @UserId AND Type = @Type;
END
GO

-- ── sp_GetIntegrationsByUser ──────────────────────────────
CREATE OR ALTER PROCEDURE sp_GetIntegrationsByUser
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM Integrations WHERE UserId = @UserId;
END
GO
