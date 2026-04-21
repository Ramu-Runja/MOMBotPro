using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MOMBotPro.API.Services;

public class ZoomTokenService
{
    private readonly ApplicationDbContext      _db;
    private readonly IHttpClientFactory        _httpFactory;
    private readonly IConfiguration            _config;
    private readonly ILogger<ZoomTokenService> _logger;

    public ZoomTokenService(
        ApplicationDbContext      db,
        IHttpClientFactory        httpFactory,
        IConfiguration            config,
        ILogger<ZoomTokenService> logger)
    {
        _db          = db;
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;
    }

    public async Task<string> GetValidAccessTokenAsync(Guid userId)
    {
        var integration = await _db.Integrations
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == "Zoom" && i.IsConnected);

        if (integration == null || string.IsNullOrEmpty(integration.AccessToken))
            throw new InvalidOperationException("Zoom not connected. Please reconnect Zoom in Integrations.");

        bool isExpired = !integration.ExpiresAt.HasValue ||
                         integration.ExpiresAt.Value <= DateTime.UtcNow.AddMinutes(5);

        if (!isExpired)
            return integration.AccessToken;

        _logger.LogInformation(
            "Zoom access token expiring soon (ExpiresAt={At}) — refreshing for userId={Id}",
            integration.ExpiresAt, userId);

        return await RefreshAsync(integration);
    }

    private async Task<string> RefreshAsync(Models.Integration integ)
    {
        if (string.IsNullOrEmpty(integ.ConfigJson))
            throw new InvalidOperationException("No Zoom refresh token stored. Please reconnect Zoom.");

        using var cfgDoc     = JsonDocument.Parse(integ.ConfigJson);
        var refreshToken     = cfgDoc.RootElement.TryGetProperty("refresh_token", out var rte)
                               ? rte.GetString() : null;

        if (string.IsNullOrEmpty(refreshToken))
            throw new InvalidOperationException("Zoom refresh token is missing. Please reconnect Zoom.");

        var clientId     = _config["Zoom:ClientId"]     ?? "";
        var clientSecret = _config["Zoom:ClientSecret"] ?? "";
        var credentials  = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        var res = await http.PostAsync(
            "https://zoom.us/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "refresh_token",
                ["refresh_token"] = refreshToken,
            }));

        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            _logger.LogError("Zoom token refresh failed {Status}: {Raw}", (int)res.StatusCode, raw);
            throw new InvalidOperationException(
                $"Zoom token refresh failed ({(int)res.StatusCode}). Please reconnect Zoom.");
        }

        using var doc        = JsonDocument.Parse(raw);
        var newAccessToken   = doc.RootElement.TryGetProperty("access_token",  out var at)
                               ? at.GetString() ?? "" : "";
        var newRefreshToken  = doc.RootElement.TryGetProperty("refresh_token", out var rt)
                               ? rt.GetString() ?? refreshToken : refreshToken;
        var expiresIn        = doc.RootElement.TryGetProperty("expires_in",    out var ei)
                               ? ei.GetInt32() : 3600;
        var scope            = doc.RootElement.TryGetProperty("scope",         out var sc)
                               ? sc.GetString() ?? "" : "";

        if (string.IsNullOrEmpty(newAccessToken))
            throw new InvalidOperationException("Zoom token refresh returned empty access_token.");

        var expiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 60);

        integ.AccessToken = newAccessToken;
        integ.ExpiresAt   = expiresAt;
        integ.ConfigJson  = JsonSerializer.Serialize(new
        {
            refresh_token = newRefreshToken,
            expiresAt     = expiresAt.ToString("o"),
            scope
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Zoom token refreshed for userId={Id}, new ExpiresAt={At}", integ.UserId, expiresAt);

        return newAccessToken;
    }
}
