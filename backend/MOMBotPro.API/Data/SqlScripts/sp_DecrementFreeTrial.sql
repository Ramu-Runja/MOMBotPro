-- =============================================
-- Stored Procedure: sp_DecrementFreeTrial
-- Description    : Decrement free trial meeting count by 1
--                  Does not go below 0
-- Used In        : UserRepository.DecrementTrialAsync()
-- =============================================

USE MOMBotProDB;
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
