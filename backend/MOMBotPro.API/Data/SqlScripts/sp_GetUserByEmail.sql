-- =============================================
-- Stored Procedure: sp_GetUserByEmail
-- Description    : Fetch a user record by email
-- Used In        : UserRepository.GetByEmailAsync()
-- =============================================

USE MOMBotProDB;
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
        Id,
        FullName,
        Email,
        PasswordHash,
        CompanyName,
        Domain,
        Role,
        SubscriptionPlan,
        FreeTrialMeetingsLeft,
        IsActive,
        CreatedAt,
        UpdatedAt
    FROM dbo.Users
    WHERE Email = @Email
      AND IsActive = 1;
END
GO
