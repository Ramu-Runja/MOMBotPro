-- =============================================
-- Stored Procedure: sp_CreateUser
-- Description    : Insert a new user and return the created record
-- Used In        : UserRepository.CreateAsync()
-- =============================================

USE MOMBotProDB;
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

    -- Prevent duplicate email
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email)
    BEGIN
        -- Return existing user if already registered
        SELECT
            Id, FullName, Email, PasswordHash, CompanyName, Domain,
            Role, SubscriptionPlan, FreeTrialMeetingsLeft, IsActive,
            CreatedAt, UpdatedAt
        FROM dbo.Users
        WHERE Email = @Email;
        RETURN;
    END

    INSERT INTO dbo.Users
        (Id, FullName, Email, PasswordHash, CompanyName, Domain,
         Role, SubscriptionPlan, FreeTrialMeetingsLeft, IsActive,
         CreatedAt, UpdatedAt)
    VALUES
        (@Id, @FullName, @Email, @PasswordHash, @CompanyName, @Domain,
         'User', 'free_trial', 3, 1,
         GETUTCDATE(), GETUTCDATE());

    -- Return the newly created record
    SELECT
        Id, FullName, Email, PasswordHash, CompanyName, Domain,
        Role, SubscriptionPlan, FreeTrialMeetingsLeft, IsActive,
        CreatedAt, UpdatedAt
    FROM dbo.Users
    WHERE Id = @Id;
END
GO
