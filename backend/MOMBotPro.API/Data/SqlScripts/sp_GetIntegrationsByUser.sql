-- =============================================
-- Stored Procedure: sp_GetIntegrationsByUser
-- Description    : Fetch all integrations for a given user
-- Used In        : UserRepository.GetIntegrationsAsync()
-- =============================================

USE MOMBotProDB;
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
        Id,
        UserId,
        Type,
        ConfigJson,
        IsConnected,
        ConnectedAt,
        Domain,
        Email,
        AccessToken,
        ProjectKey,
        Owner,
        Repo,
        AccountId
    FROM dbo.Integrations
    WHERE UserId = @UserId;
END
GO
