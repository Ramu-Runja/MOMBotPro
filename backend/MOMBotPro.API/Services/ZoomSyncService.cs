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
        var integ = await _db.Integrations
            .FirstOrDefaultAsync(i =>
                i.UserId == userId && i.Type == "Zoom" && i.IsConnected, ct);

        if (integ == null)
        {
            _logger.LogDebug("No active Zoom integration for user {UserId}", userId);
            return;
        }

        var meetings = await FetchMeetingsAsync(integ, ct);
        if (meetings == null)
        {
            _logger.LogWarning("Zoom API returned no meetings for user {UserId}", userId);
            return;
        }

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
                // Don't overwrite meetings already in-flight or done
                existing.Topic      = m.Topic;
                existing.StartTime  = m.StartTime;
                existing.Duration   = m.Duration;
                existing.JoinUrl    = m.JoinUrl;
                existing.IsRecurring = m.IsRecurring;
                updated++;
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Zoom sync for user {UserId}: +{Added} new, {Updated} updated",
            userId, added, updated);
    }

    // ── Fetch meetings from Zoom API (retry once on 401) ─────────────────
    private async Task<List<ZoomMeetingData>?> FetchMeetingsAsync(
        Integration integ, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient();
        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", integ.AccessToken);

        var res = await http.GetAsync(
            "https://api.zoom.us/v2/users/me/meetings?type=scheduled&page_size=300", ct);

        if (res.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogInformation("Zoom 401 — refreshing token for user {UserId}", integ.UserId);
            var newToken = await RefreshAccessTokenAsync(integ, ct);
            if (string.IsNullOrEmpty(newToken))
            {
                _logger.LogWarning("Zoom token refresh failed for user {UserId}", integ.UserId);
                return null;
            }
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", newToken);
            res = await http.GetAsync(
                "https://api.zoom.us/v2/users/me/meetings?type=scheduled&page_size=300", ct);
        }

        if (!res.IsSuccessStatusCode)
        {
            var raw = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Zoom meetings API {Status}: {Raw}", (int)res.StatusCode, raw);
            return null;
        }

        var json    = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        var list = new List<ZoomMeetingData>();

        if (!doc.RootElement.TryGetProperty("meetings", out var arr))
            return list;

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
                // type 3 = recurring_no_time, 8 = recurring_with_time
                var isRecurring = meetingType is 3 or 8;

                if (!string.IsNullOrEmpty(meetingId))
                    list.Add(new ZoomMeetingData(meetingId, topic, startTime, duration, joinUrl, isRecurring));
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Skipping malformed Zoom meeting: {Msg}", ex.Message);
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
            _logger.LogWarning("Zoom token refresh HTTP {Status}", (int)res.StatusCode);
            return null;
        }

        var raw = await res.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(raw);

        var newAccess  = doc.RootElement.TryGetProperty("access_token",  out var at) ? at.GetString() : null;
        var newRefresh = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

        if (string.IsNullOrEmpty(newAccess)) return null;

        // Persist updated tokens
        integ.AccessToken = newAccess;
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
