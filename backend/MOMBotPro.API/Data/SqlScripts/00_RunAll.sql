-- =============================================
-- MOMBotPro — Run All Stored Procedures
-- Description : Execute this file in SSMS to create
--               all stored procedures in one shot.
--
-- PREREQUISITES:
--   1. Run EF Migrations first to create all tables:
--      dotnet ef database update
--   2. Then run THIS script in SSMS against MOMBotProDB
--
-- ORDER MATTERS — run in sequence below
-- =============================================

USE MOMBotProDB;
GO

PRINT '== MOMBotPro Stored Procedures Setup ==';
PRINT 'Starting at: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '';

-- ── 1. sp_GetUserByEmail ─────────────────────
PRINT 'Creating sp_GetUserByEmail...';
GO

IF OBJECT_ID('dbo.sp_GetUserByEmail', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetUserByEmail;
GO

CREATE PROCEDURE dbo.sp_GetUserByEmail
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        Id, FullName, Email, PasswordHash, CompanyName, Domain,
        Role, SubscriptionPlan, FreeTrialMeetingsLeft, IsActive,
        CreatedAt, UpdatedAt
    FROM dbo.Users
    WHERE Email = @Email AND IsActive = 1;
END
GO

PRINT 'sp_GetUserByEmail created successfully.';
GO

-- ── 2. sp_GetUserById ────────────────────────
PRINT 'Creating sp_GetUserById...';
GO

IF OBJECT_ID('dbo.sp_GetUserById', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetUserById;
GO

CREATE PROCEDURE dbo.sp_GetUserById
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        Id, FullName, Email, PasswordHash, CompanyName, Domain,
        Role, SubscriptionPlan, FreeTrialMeetingsLeft, IsActive,
        CreatedAt, UpdatedAt
    FROM dbo.Users
    WHERE Id = @Id AND IsActive = 1;
END
GO

PRINT 'sp_GetUserById created successfully.';
GO

-- ── 3. sp_CreateUser ─────────────────────────
PRINT 'Creating sp_CreateUser...';
GO

IF OBJECT_ID('dbo.sp_CreateUser', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_CreateUser;
GO

CREATE PROCEDURE dbo.sp_CreateUser
    @Id           UNIQUEIDENTIFIER,
    @FullName     NVARCHAR(256),
    @Email        NVARCHAR(256),
    @PasswordHash NVARCHAR(512),
    @CompanyName  NVARCHAR(256),
    @Domain       NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email)
    BEGIN
        SELECT
            Id, FullName, Email, PasswordHash, CompanyName, Domain,
            Role, SubscriptionPlan, FreeTrialMeetingsLeft, IsActive,
            CreatedAt, UpdatedAt
        FROM dbo.Users WHERE Email = @Email;
        RETURN;
    END

    INSERT INTO dbo.Users
        (Id, FullName, Email, PasswordHash, CompanyName, Domain,
         Role, SubscriptionPlan, FreeTrialMeetingsLeft, IsActive,
         CreatedAt, UpdatedAt)
    VALUES
        (@Id, @FullName, @Email, @PasswordHash, @CompanyName, @Domain,
         'User', 'free_trial', 3, 1, GETUTCDATE(), GETUTCDATE());

    SELECT
        Id, FullName, Email, PasswordHash, CompanyName, Domain,
        Role, SubscriptionPlan, FreeTrialMeetingsLeft, IsActive,
        CreatedAt, UpdatedAt
    FROM dbo.Users WHERE Id = @Id;
END
GO

PRINT 'sp_CreateUser created successfully.';
GO

-- ── 4. sp_DecrementFreeTrial ──────────────────
PRINT 'Creating sp_DecrementFreeTrial...';
GO

IF OBJECT_ID('dbo.sp_DecrementFreeTrial', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_DecrementFreeTrial;
GO

CREATE PROCEDURE dbo.sp_DecrementFreeTrial
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
    SET
        FreeTrialMeetingsLeft = CASE
            WHEN FreeTrialMeetingsLeft > 0 THEN FreeTrialMeetingsLeft - 1
            ELSE 0
        END,
        UpdatedAt = GETUTCDATE()
    WHERE Id = @UserId;
END
GO

PRINT 'sp_DecrementFreeTrial created successfully.';
GO

-- ── 5. sp_UpdateSubscription ──────────────────
PRINT 'Creating sp_UpdateSubscription...';
GO

IF OBJECT_ID('dbo.sp_UpdateSubscription', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_UpdateSubscription;
GO

CREATE PROCEDURE dbo.sp_UpdateSubscription
    @UserId   UNIQUEIDENTIFIER,
    @Plan     NVARCHAR(50),
    @PriceUSD DECIMAL(10, 2),
    @PriceINR DECIMAL(10, 2)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Users
    SET
        SubscriptionPlan      = @Plan,
        FreeTrialMeetingsLeft = 9999,
        UpdatedAt             = GETUTCDATE()
    WHERE Id = @UserId;
END
GO

PRINT 'sp_UpdateSubscription created successfully.';
GO

-- ── 6. sp_GetIntegrationsByUser ───────────────
PRINT 'Creating sp_GetIntegrationsByUser...';
GO

IF OBJECT_ID('dbo.sp_GetIntegrationsByUser', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetIntegrationsByUser;
GO

CREATE PROCEDURE dbo.sp_GetIntegrationsByUser
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        Id, UserId, Type, ConfigJson, IsConnected, ConnectedAt,
        Domain, Email, AccessToken, ProjectKey, Owner, Repo, AccountId
    FROM dbo.Integrations
    WHERE UserId = @UserId;
END
GO

PRINT 'sp_GetIntegrationsByUser created successfully.';
GO

-- ── 7. sp_SaveIntegration ─────────────────────
PRINT 'Creating sp_SaveIntegration...';
GO

IF OBJECT_ID('dbo.sp_SaveIntegration', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SaveIntegration;
GO

CREATE PROCEDURE dbo.sp_SaveIntegration
    @UserId      UNIQUEIDENTIFIER,
    @Type        NVARCHAR(50),
    @ConfigJson  NVARCHAR(MAX),
    @IsConnected BIT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (
        SELECT 1 FROM dbo.Integrations
        WHERE UserId = @UserId AND Type = @Type
    )
    BEGIN
        UPDATE dbo.Integrations
        SET
            ConfigJson  = @ConfigJson,
            IsConnected = @IsConnected,
            ConnectedAt = CASE WHEN @IsConnected = 1 THEN GETUTCDATE() ELSE ConnectedAt END,
            UpdatedAt   = GETUTCDATE()
        WHERE UserId = @UserId AND Type = @Type;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.Integrations
            (Id, UserId, Type, ConfigJson, IsConnected, ConnectedAt,
             CreatedAt, UpdatedAt)
        VALUES
            (NEWID(), @UserId, @Type, @ConfigJson, @IsConnected,
             CASE WHEN @IsConnected = 1 THEN GETUTCDATE() ELSE NULL END,
             GETUTCDATE(), GETUTCDATE());
    END

    SELECT
        Id, UserId, Type, ConfigJson, IsConnected, ConnectedAt,
        Domain, Email, AccessToken, ProjectKey, Owner, Repo, AccountId
    FROM dbo.Integrations
    WHERE UserId = @UserId AND Type = @Type;
END
GO

PRINT 'sp_SaveIntegration created successfully.';
GO

-- ── Done ──────────────────────────────────────
PRINT '';
PRINT '== All stored procedures created successfully! ==';
PRINT 'Completed at: ' + CONVERT(VARCHAR, GETDATE(), 120);
GO
