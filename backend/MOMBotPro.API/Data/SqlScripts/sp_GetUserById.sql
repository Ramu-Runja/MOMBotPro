-- =============================================
-- Stored Procedure: sp_GetUserById
-- Description    : Fetch a user record by GUID ID
-- Used In        : UserRepository.GetByIdAsync()
-- =============================================

USE MOMBotProDB;
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
    WHERE Id = @Id
      AND IsActive = 1;
END
GO
