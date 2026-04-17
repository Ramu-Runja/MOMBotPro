-- =============================================
-- Stored Procedure: sp_UpdateSubscription
-- Description    : Upgrade a user's subscription plan
--                  Sets FreeTrialMeetingsLeft to 9999 for paid plans
-- Used In        : UserRepository.UpgradePlanAsync()
-- =============================================

USE MOMBotProDB;
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
