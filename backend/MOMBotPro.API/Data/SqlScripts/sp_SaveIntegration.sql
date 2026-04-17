-- =============================================
-- Stored Procedure: sp_SaveIntegration
-- Description    : Upsert an integration record for a user.
--                  If the (UserId, Type) pair exists → UPDATE
--                  Otherwise → INSERT
--                  Setting IsConnected = 0 effectively "disconnects"
-- Used In        : UserRepository.SaveIntegrationAsync()
--                  UserRepository.DeleteIntegrationAsync()
-- =============================================

USE MOMBotProDB;
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
        -- UPDATE existing row
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
        -- INSERT new row
        INSERT INTO dbo.Integrations
            (Id, UserId, Type, ConfigJson, IsConnected, ConnectedAt,
             CreatedAt, UpdatedAt)
        VALUES
            (NEWID(), @UserId, @Type, @ConfigJson, @IsConnected,
             CASE WHEN @IsConnected = 1 THEN GETUTCDATE() ELSE NULL END,
             GETUTCDATE(), GETUTCDATE());
    END

    -- Return the saved record
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
    WHERE UserId = @UserId AND Type = @Type;
END
GO
