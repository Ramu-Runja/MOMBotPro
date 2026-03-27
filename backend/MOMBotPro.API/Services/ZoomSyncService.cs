using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MOMBotPro.API.Services;

/// <summary>
/// Fetches scheduled meetings from Zoom API and upserts them into ZoomMeetings table.
/// Handles token refresh on 401 using the refresh_token stored in Integration.ConfigJson.
/// </summary>
public class ZoomSyncService
{
    private readonly ApplicationDbContext    _db;
    private readonly IHttpClientFactory      _httpFactory;
    private readonly IConfiguration          _config;
    private readonly ILogger<ZoomSyncService> _logger;

    public ZoomSyncService(
        ApplicationDbContext     db,
        IHttpClientFactory       httpFactory,
        IConfiguration           config,
        ILogger<ZoomSyncService> logger)
    {
        _db          = db;
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;
    }

    // ── Public entry point ────────────────────────────────────────────────
    public async Task FetchAndSyncMeetingsAsync(
        Guid userId, CancellationToken ct = default)
    {
        try
        {
            // Step 1 — find the Zoom integration row
            var integ = await _db.Integrations
                .FirstOrDefaultAsync(i =>
                    i.UserId == userId && i.Type == "Zoom" && i.IsConnected, ct);

            if (integ == null)
            {
                _logger.LogWarning("No active Zoom integration found for userId: {Id}", userId);
                return;
            }

            _logger.LogInformation(
                "Found Zoom integration for userId: {Id} — accessToken null={Null}",
                integ.UserId, integ.AccessToken == null);

            // Step 2 — always refresh when access token is missing;
            //          also refresh proactively when ExpiresAt is near.
            var accessToken = integ.AccessToken;
            bool needsRefresh = string.IsNullOrEmpty(accessToken) ||
                                (integ.ExpiresAt.HasValue &&
                                 integ.ExpiresAt.Value <= DateTime.UtcNow.AddMinutes(5));

            if (needsRefresh)
            {
                _logger.LogInformation(
                    "Access token null or expiring — refreshing for userId: {Id}", userId);
                var refreshed = await RefreshAccessTokenAsync(integ, ct);
                if (string.IsNullOrEmpty(refreshed))
                {
                    _logger.LogError(
                        "Zoom token refresh failed for userId: {Id} — aborting sync", userId);
                    return;
                }
                accessToken = refreshed;
                _logger.LogInformation("Token refreshed successfully for userId: {Id}", userId);
            }

            _logger.LogInformation("Got access token — calling Zoom API...");

            // Step 3 — fetch meetings from Zoom API
            var meetings = await FetchMeetingsAsync(accessToken, integ, ct);
            if (meetings == null)
            {
                throw new InvalidOperationException(
                    "Zoom meetings API call failed — check backend logs for HTTP status/error details.");
            }

            _logger.LogInformation(
                "Zoom API returned {Count} meetings for userId: {Id}", meetings.Count, userId);

            // Step 5 — upsert into ZoomMeetings table
            int added = 0, updated = 0;

            foreach (var m in meetings)
            {
                var existing = await _db.ZoomMeetings
                    .FirstOrDefaultAsync(z =>
                        z.UserId == userId && z.ZoomMeetingId == m.ZoomMeetingId, ct);

                if (existing == null)
                {
                    _db.ZoomMeetings.Add(new ZoomMeeting
                    {
                        Id            = Guid.NewGuid(),
                        UserId        = userId,
                        ZoomMeetingId = m.ZoomMeetingId,
                        Topic         = m.Topic,
                        StartTime     = m.StartTime,
                        Duration      = m.Duration,
                        JoinUrl       = m.JoinUrl,
                        Status        = "scheduled",
                        IsRecurring   = m.IsRecurring,
                        CreatedAt     = DateTime.UtcNow,
                    });
                    added++;
                }
                else if (existing.Status != "bot_joining" && existing.Status != "ended")
                {
                    existing.Topic       = m.Topic;
                    existing.StartTime   = m.StartTime;
                    existing.Duration    = m.Duration;
                    existing.JoinUrl     = m.JoinUrl;
                    existing.IsRecurring = m.IsRecurring;
                    updated++;
                }
            }

            _logger.LogInformation(
                "Zoom sync: saving {Count} meetings to DB (new={Added}, updated={Updated})",
                added + updated, added, updated);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Saved {Count} meetings to DB for userId: {Id}",
                added + updated, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zoom sync failed: {Message}", ex.Message);
            throw;
        }
    }

    // ── Fetch meetings from Zoom API ──────────────────────────────────────
    // accessToken is guaranteed non-null/non-empty by the caller.
    private async Task<List<ZoomMeetingData>?> FetchMeetingsAsync(
        string accessToken, Integration integ, CancellationToken ct)
    {
        const string url = "https://api.zoom.us/v2/users/me/meetings?type=scheduled&page_size=100";

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        _logger.LogInformation("GET {Url}", url);
        var res = await http.GetAsync(url, ct);
        _logger.LogInformation("Zoom API response: {Status}", (int)res.StatusCode);

        // On 401 try one more refresh (token may have expired mid-flight)
        if (res.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogInformation("Zoom 401 — refreshing token for userId: {Id}", integ.UserId);
            var newToken = await RefreshAccessTokenAsync(integ, ct);
            if (string.IsNullOrEmpty(newToken))
            {
                throw new InvalidOperationException(
                    "Zoom token refresh failed — token may be expired or revoked. Please reconnect Zoom.");
            }
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", newToken);
            res = await http.GetAsync(url, ct);
            _logger.LogInformation("Zoom API retry response: {Status}", (int)res.StatusCode);
        }

        if (!res.IsSuccessStatusCode)
        {
            var raw = await res.Content.ReadAsStringAsync(ct);
            _logger.LogError("Zoom meetings API {Status}: {Raw}", (int)res.StatusCode, raw);
            throw new InvalidOperationException(
                $"Zoom API returned {(int)res.StatusCode}: {raw[..Math.Min(300, raw.Length)]}");
        }

        var json = await res.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("Zoom API raw response: {Json}", json[..Math.Min(500, json.Length)]);

        using var doc = JsonDocument.Parse(json);
        var list = new List<ZoomMeetingData>();

        if (!doc.RootElement.TryGetProperty("meetings", out var arr))
        {
            _logger.LogWarning("Zoom response has no 'meetings' key — full response: {Json}",
                json[..Math.Min(500, json.Length)]);
            return list;
        }

        foreach (var m in arr.EnumerateArray())
        {
            try
            {
                var meetingId = m.TryGetProperty("id", out var idEl)
                    ? idEl.GetRawText().Trim('"') : "";

                var topic = m.TryGetProperty("topic", out var t) ? t.GetString() ?? "" : "";

                DateTime startTime = DateTime.UtcNow;
                if (m.TryGetProperty("start_time", out var st) && st.ValueKind == JsonValueKind.String)
                    DateTime.TryParse(st.GetString(), null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out startTime);

                var duration    = m.TryGetProperty("duration", out var d) ? d.GetInt32() : 60;
                var joinUrl     = m.TryGetProperty("join_url", out var ju) ? ju.GetString() ?? "" : "";
                var meetingType = m.TryGetProperty("type", out var ty) ? ty.GetInt32() : 2;
                var isRecurring = meetingType is 3 or 8;

                if (!string.IsNullOrEmpty(meetingId))
                    list.Add(new ZoomMeetingData(meetingId, topic, startTime, duration, joinUrl, isRecurring));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Skipping malformed Zoom meeting entry: {Msg}", ex.Message);
            }
        }

        return list;
    }

    // ── Refresh access token (same pattern as Jira) ───────────────────────
    private async Task<string?> RefreshAccessTokenAsync(
        Integration integ, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(integ.ConfigJson)) return null;

        using var cfgDoc = JsonDocument.Parse(integ.ConfigJson);
        var refreshToken = cfgDoc.RootElement.TryGetProperty("refresh_token", out var rte)
                           ? rte.GetString() : null;

        if (string.IsNullOrEmpty(refreshToken)) return null;

        var clientId     = _config["Zoom:ClientId"]     ?? "";
        var clientSecret = _config["Zoom:ClientSecret"] ?? "";
        var credentials  = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", credentials);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "refresh_token",
            ["refresh_token"] = refreshToken,
        });

        var res = await http.PostAsync("https://zoom.us/oauth/token", form, ct);
        if (!res.IsSuccessStatusCode)
        {
            if (res.StatusCode == HttpStatusCode.Unauthorized)
                _logger.LogError(
                    "Zoom refresh token is invalid or revoked for userId: {Id} — user must reconnect Zoom",
                    integ.UserId);
            else
                _logger.LogError("Zoom token refresh HTTP {Status} for userId: {Id}",
                    (int)res.StatusCode, integ.UserId);
            return null;
        }
        _logger.LogInformation("Zoom token refreshed successfully for userId: {Id}", integ.UserId);

        var raw = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(raw);

        var newAccess  = doc.RootElement.TryGetProperty("access_token",  out var at) ? at.GetString() : null;
        var newRefresh = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        if (string.IsNullOrEmpty(newAccess)) return null;

        // Persist updated tokens
        integ.AccessToken = newAccess;
        integ.ExpiresAt   = DateTime.UtcNow.AddHours(1); // Zoom tokens expire in ~1 hour
        integ.ConfigJson  = JsonSerializer.Serialize(
            new { refresh_token = newRefresh ?? refreshToken });
        await _db.SaveChangesAsync(ct);

        return newAccess;
    }

    private record ZoomMeetingData(
        string   ZoomMeetingId,
        string   Topic,
        DateTime StartTime,
        int      Duration,
        string   JoinUrl,
        bool     IsRecurring);
}
