namespace MOMBotPro.API.Services;

public interface ITokenProvider
{
    /// <summary>
    /// Returns a valid access token for the given provider and user.
    /// Automatically refreshes when the stored token is expired.
    /// Pass forceRefresh=true to skip the expiry check and always refresh.
    /// Supported providers: "Jira", "GitHub", "Zoom", "Gmail"
    /// </summary>
    Task<string> GetAccessTokenAsync(string provider, Guid userId, bool forceRefresh = false);
}
