using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Services;

public class TokenProvider : ITokenProvider
{
    private readonly ApplicationDbContext    _db;
    private readonly IHttpClientFactory      _httpFactory;
    private readonly IConfiguration          _config;
    private readonly AesEncryptor            _aes;
    private readonly ILogger<TokenProvider>  _logger;

    public TokenProvider(
        ApplicationDbContext    db,
        IHttpClientFactory      httpFactory,
        IConfiguration          config,
        AesEncryptor            aes,
        ILogger<TokenProvider>  logger)
    {
        _db          = db;
        _httpFactory = httpFactory;
        _config      = config;
        _aes         = aes;
        _logger      = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────

    public async Task<string> GetAccessTokenAsync(
        string provider, Guid userId, bool forceRefresh = false)
    {
        var integration = await _db.Integrations
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == provider)
            ?? throw new InvalidOperationException(
                $"No {provider} integration found for user {userId}");

        // Gmail stores its refresh_token in the AccessToken field and uses a
        // one-shot exchange each time — access tokens are never persisted.
        if (provider == "Gmail")
            return await ExchangeGmailRefreshTokenAsync(integration);

        var storedToken = _aes.Decrypt(integration.AccessToken ?? "") ?? "";

        // Return early if token is still valid (with 5-minute buffer)
        if (!forceRefresh &&
            integration.ExpiresAt.HasValue &&
            integration.ExpiresAt.Value > DateTime.UtcNow.AddMinutes(5))
        {
            return storedToken;
        }

        // Find refresh token in ConfigJson
        var refreshToken = ExtractRefreshToken(integration.ConfigJson);
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogDebug(
                "{Provider} integration has no refresh token — returning stored token", provider);
            return storedToken;
        }

        // Exchange refresh token for a new access token
        try
        {
            var (newToken, newRefresh, expiresIn) =
                await CallRefreshEndpointAsync(provider, refreshToken);

            integration.AccessToken = _aes.Encrypt(newToken);
            integration.ExpiresAt   = DateTime.UtcNow.AddSeconds(expiresIn);

            if (!string.IsNullOrEmpty(newRefresh))
                integration.ConfigJson = UpdateConfigJson(integration.ConfigJson, newRefresh);

            await _db.SaveChangesAsync();
            _logger.LogInformation("{Provider} token refreshed for user {UserId}", provider, userId);

            return newToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Provider} token refresh failed for user {UserId} — returning stale token",
                provider, userId);
            return storedToken;   // best-effort fallback
        }
    }

    // ── Provider-specific refresh ─────────────────────────────────────────

    private async Task<(string token, string? newRefresh, int expiresIn)>
        CallRefreshEndpointAsync(string provider, string refreshToken)
    {
        using var http = _httpFactory.CreateClient();

        HttpResponseMessage res = provider switch
        {
            "Jira" => await http.PostAsJsonAsync(
                "https://auth.atlassian.com/oauth/token",
                new
                {
                    grant_type    = "refresh_token",
                    client_id     = _config["Jira:ClientId"]     ?? "",
                    client_secret = _config["Jira:ClientSecret"] ?? "",
                    refresh_token = refreshToken
                }),

            "GitHub" => await PostFormAsync(http,
                "https://github.com/login/oauth/access_token",
                new Dictionary<string, string>
                {
                    ["grant_type"]    = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"]     = _config["GitHub:ClientId"]     ?? "",
                    ["client_secret"] = _config["GitHub:ClientSecret"] ?? ""
                },
                acceptJson: true),

            "Zoom" => await PostFormWithBasicAuthAsync(http,
                "https://zoom.us/oauth/token",
                _config["Zoom:ClientId"]     ?? "",
                _config["Zoom:ClientSecret"] ?? "",
                new Dictionary<string, string>
                {
                    ["grant_type"]    = "refresh_token",
                    ["refresh_token"] = refreshToken
                }),

            _ => throw new NotSupportedException(
                $"Token refresh not supported for provider: {provider}")
        };

        var raw = await res.Content.ReadAsStringAsync();
        _logger.LogDebug("{Provider} refresh response: {Raw}",
            provider, raw[..Math.Min(200, raw.Length)]);

        using var doc  = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        if (!root.TryGetProperty("access_token", out var atEl))
            throw new InvalidOperationException(
                $"{provider} token refresh returned no access_token: " +
                raw[..Math.Min(200, raw.Length)]);

        var token      = atEl.GetString()
            ?? throw new InvalidOperationException($"{provider} access_token is null");
        var newRefresh = root.TryGetProperty("refresh_token", out var rtEl)
            ? rtEl.GetString() : null;
        var expiresIn  = root.TryGetProperty("expires_in", out var eiEl)
            ? eiEl.GetInt32() : 3600;

        return (token, newRefresh, expiresIn);
    }

    /// <summary>
    /// Gmail stores the refresh_token directly in the AccessToken column.
    /// Exchange it for a short-lived access token without persisting the result.
    /// </summary>
    private async Task<string> ExchangeGmailRefreshTokenAsync(Integration integration)
    {
        var refreshToken = _aes.Decrypt(integration.AccessToken ?? "");
        if (string.IsNullOrEmpty(refreshToken))
            return "";

        using var http = _httpFactory.CreateClient();
        var res = await PostFormAsync(http,
            "https://oauth2.googleapis.com/token",
            new Dictionary<string, string>
            {
                ["grant_type"]    = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"]     = _config["Google:ClientId"]     ?? "",
                ["client_secret"] = _config["Google:ClientSecret"] ?? ""
            });

        var raw = await res.Content.ReadAsStringAsync();
        _logger.LogDebug("Gmail token exchange response: {Raw}", raw[..Math.Min(200, raw.Length)]);

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.TryGetProperty("access_token", out var at)
            ? at.GetString() ?? ""
            : "";
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────

    private static Task<HttpResponseMessage> PostFormAsync(
        HttpClient http,
        string url,
        Dictionary<string, string> form,
        bool acceptJson = false)
    {
        if (acceptJson)
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        return http.PostAsync(url, new FormUrlEncodedContent(form));
    }

    private static Task<HttpResponseMessage> PostFormWithBasicAuthAsync(
        HttpClient http,
        string url,
        string clientId,
        string clientSecret,
        Dictionary<string, string> form)
    {
        var creds = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", creds);
        return http.PostAsync(url, new FormUrlEncodedContent(form));
    }

    // ── ConfigJson helpers ────────────────────────────────────────────────

    private static string? ExtractRefreshToken(string? configJson)
    {
        if (string.IsNullOrEmpty(configJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(configJson);
            return doc.RootElement.TryGetProperty("refresh_token", out var el)
                ? el.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string UpdateConfigJson(string? existing, string newRefreshToken)
    {
        var dict = new Dictionary<string, string> { ["refresh_token"] = newRefreshToken };

        if (!string.IsNullOrEmpty(existing))
        {
            try
            {
                using var doc = JsonDocument.Parse(existing);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    if (prop.Name != "refresh_token")
                        dict[prop.Name] = prop.Value.GetString() ?? "";
            }
            catch { /* ignore malformed JSON, just overwrite */ }
        }

        return JsonSerializer.Serialize(dict);
    }
}
