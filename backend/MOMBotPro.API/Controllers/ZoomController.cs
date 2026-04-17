using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models;
using MOMBotPro.API.Services;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MOMBotPro.API.Controllers;

[ApiController]
[Route("api/zoom")]
[Authorize]
public class ZoomController : ControllerBase
{
    private readonly ZoomSessionRepository  _sessions;
    private readonly RecallService          _recall;
    private readonly PipelineRepository     _pipelines;
    private readonly IServiceScopeFactory   _scopeFactory;
    private readonly ZoomSyncService        _zoomSync;
    private readonly ApplicationDbContext   _db;
    private readonly IConfiguration         _config;
    private readonly ILogger<ZoomController> _logger;

    public ZoomController(
        ZoomSessionRepository    sessions,
        RecallService            recall,
        PipelineRepository       pipelines,
        IServiceScopeFactory     scopeFactory,
        ZoomSyncService          zoomSync,
        ApplicationDbContext     db,
        IConfiguration           config,
        ILogger<ZoomController>  logger)
    {
        _sessions     = sessions;
        _recall       = recall;
        _pipelines    = pipelines;
        _scopeFactory = scopeFactory;
        _zoomSync     = zoomSync;
        _db           = db;
        _config       = config;
        _logger       = logger;
    }

    // ── POST /api/zoom/join ──────────────────────────────────────────────
    [HttpPost("join")]
    public async Task<IActionResult> JoinMeeting([FromBody] JoinZoomRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ClientName))
            return BadRequest(new { error = "ClientName is required." });
        if (string.IsNullOrWhiteSpace(req.MeetingUrl))
            return BadRequest(new { error = "MeetingUrl is required." });

        try
        {
            // URL cleaning is handled inside RecallService.CreateBot
            var botId   = await _recall.CreateBot(req.MeetingUrl);
            var session = _sessions.Create(botId, req.MeetingUrl, req.ClientName);
            return Ok(session);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── GET /api/zoom/session/{id} ───────────────────────────────────────
    [HttpGet("session/{id}")]
    public async Task<IActionResult> GetSession(string id)
    {
        var session = _sessions.Get(id);
        if (session == null) return NotFound();

        // Already terminal — just return without hitting Recall.ai again
        if (session.Status is ZoomSessionStatus.Done or ZoomSessionStatus.Failed)
            return Ok(session);

        try
        {
            var code            = await _recall.GetBotStatus(session.BotId);
            bool transcriptReady = code is "done" or "recording_done" or "completed"
                                        or "media_expired" or "ready";

            // Map Recall.ai status codes → our enum
            session.Status = code switch
            {
                "joining_call"          => ZoomSessionStatus.Joining,
                "in_call_not_recording" => ZoomSessionStatus.InCall,
                "in_call_recording"     => ZoomSessionStatus.Recording,
                "call_ended"            => ZoomSessionStatus.Processing,
                "done"                  => ZoomSessionStatus.Done,
                "recording_done"        => ZoomSessionStatus.Done,
                "completed"             => ZoomSessionStatus.Done,
                "fatal"                 => ZoomSessionStatus.Failed,
                "kicked"                => ZoomSessionStatus.Failed,
                _                       => session.Status
            };

            session.StatusMessage = code switch
            {
                "joining_call"          => "Bot is joining the Zoom call...",
                "in_call_not_recording" => "Bot is in the call, waiting for recording to start...",
                "in_call_recording"     => "Recording in progress...",
                "call_ended"            => "Call ended — processing recording...",
                "done"                  => "Recording processed, launching pipeline!",
                "recording_done"        => "Recording processed, launching pipeline!",
                "completed"             => "Recording processed, launching pipeline!",
                "fatal"                 => "Bot error: fatal. Make sure the Zoom meeting is active and the join URL is correct (format: https://zoom.us/j/MEETINGID?pwd=XXX).",
                "kicked"                => "Bot was removed from the meeting. Check that the host allows bots/participants.",
                _                       => session.StatusMessage
            };

            // Auto-start pipeline once transcript is ready (only once)
            if (transcriptReady && session.PipelineId == null)
            {
                var transcript = await _recall.GetTranscript(session.BotId);

                // Block empty transcript — mark failed instead of running on empty data
                if (string.IsNullOrWhiteSpace(transcript))
                {
                    session.Status        = ZoomSessionStatus.Failed;
                    session.StatusMessage = "No transcript captured. Ensure participants spoke during the call and cloud recording was not disabled in Zoom.";
                    await _sessions.UpdateAsync(session);
                    return Ok(session);
                }

                session.Transcript    = transcript;
                session.Status        = ZoomSessionStatus.Done;
                session.StatusMessage = "Pipeline started!";

                var userId   = GetUserId();
                var pipeline = _pipelines.Create(session.ClientName, userId);
                session.PipelineId = pipeline.Id;

                // Persist updated session to DB
                await _sessions.UpdateAsync(session);

                // Fire-and-forget pipeline using a fresh DI scope (avoids scope disposal in background task)
                var capturedPipelineId  = pipeline.Id;
                var capturedTranscript  = transcript;
                var capturedClientName  = session.ClientName;
                var capturedBotId       = session.BotId;
                var capturedUserId      = userId;

                _ = Task.Run(async () =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<PipelineOrchestrator>();
                    await orchestrator.RunAsync(capturedPipelineId, capturedTranscript, capturedClientName, capturedBotId, capturedUserId);
                });
            }
            else
            {
                await _sessions.UpdateAsync(session);
            }
        }
        catch (Exception ex)
        {
            session.StatusMessage = $"Status check failed: {ex.Message}";
        }

        return Ok(session);
    }

    // ── GET /api/zoom/sessions ───────────────────────────────────────────
    [HttpGet("sessions")]
    public IActionResult GetSessions() => Ok(_sessions.GetAll());

    // ── GET /api/zoom/meetings ───────────────────────────────────────────
    // Returns all synced Zoom calendar meetings for the current user.
    [HttpGet("meetings")]
    public async Task<IActionResult> GetMeetings()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        _logger.LogInformation("Fetching meetings for userId: {Id}", userId);

        var meetings = await _db.ZoomMeetings
            .Where(m => m.UserId == userId.Value || m.UserId == null)
            .OrderBy(m => m.StartTime)
            .Select(m => new
            {
                m.Id,
                m.ZoomMeetingId,
                m.Topic,
                m.StartTime,
                m.Duration,
                m.JoinUrl,
                m.Status,
                m.IsRecurring,
            })
            .ToListAsync();

        _logger.LogInformation("Fetching meetings for userId={Id}, found={Count}", userId, meetings.Count);
        return Ok(meetings);
    }

    // ── POST /api/zoom/create-meeting ───────────────────────────────────
    [HttpPost("create-meeting")]
    public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Topic))
            return BadRequest(new { error = "Topic is required." });

        var integ = await _db.Integrations
            .FirstOrDefaultAsync(i => i.UserId == userId.Value && i.Type == "Zoom" && i.IsConnected);

        if (integ == null)
            return BadRequest(new { error = "Zoom not connected. Connect Zoom first." });

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", integ.AccessToken);
        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var zoomPayload = new
        {
            topic      = req.Topic,
            type       = 2, // scheduled
            start_time = req.StartTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            duration   = req.Duration,
            timezone   = "UTC",
            settings   = new { host_video = true, participant_video = true, waiting_room = false }
        };

        var res  = await http.PostAsJsonAsync("https://api.zoom.us/v2/users/me/meetings", zoomPayload);
        var body = await res.Content.ReadAsStringAsync();
        _logger.LogInformation("Zoom create-meeting {Status}: {Body}",
            (int)res.StatusCode, body[..Math.Min(500, body.Length)]);

        if (!res.IsSuccessStatusCode)
            return StatusCode((int)res.StatusCode, new { error = $"Zoom API error: {body}" });

        using var doc     = JsonDocument.Parse(body);
        var root          = doc.RootElement;
        var zoomMeetingId = root.TryGetProperty("id",       out var mid)
                            ? mid.GetInt64().ToString() : Guid.NewGuid().ToString();
        var joinUrl       = root.TryGetProperty("join_url", out var ju)
                            ? ju.GetString() ?? "" : "";

        var meeting = new ZoomMeeting
        {
            Id            = Guid.NewGuid(),
            UserId        = userId.Value,
            ZoomMeetingId = zoomMeetingId,
            Topic         = req.Topic,
            StartTime     = req.StartTime.ToUniversalTime(),
            Duration      = req.Duration,
            JoinUrl       = joinUrl,
            Status        = "scheduled",
            IsRecurring   = false,
            CreatedAt     = DateTime.UtcNow,
        };
        _db.ZoomMeetings.Add(meeting);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            id        = meeting.Id,
            topic     = meeting.Topic,
            startTime = meeting.StartTime,
            duration  = meeting.Duration,
            joinUrl   = meeting.JoinUrl,
            status    = meeting.Status,
        });
    }

    // ── GET /api/zoom/debug ──────────────────────────────────────────────
    // Calls Zoom API directly and returns raw response for diagnosis.
    [HttpGet("debug")]
    public async Task<IActionResult> DebugZoom()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var integ = await _db.Integrations
            .FirstOrDefaultAsync(i => i.UserId == userId.Value && i.Type == "Zoom" && i.IsConnected);

        if (integ == null)
            return NotFound(new { error = "No Zoom integration found." });

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", integ.AccessToken);
        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        var res = await http.GetAsync("https://api.zoom.us/v2/users/me/meetings?type=scheduled&page_size=10");
        var body = await res.Content.ReadAsStringAsync();

        return Ok(new
        {
            status   = (int)res.StatusCode,
            tokenLen = integ.AccessToken?.Length ?? 0,
            hasRefreshToken = !string.IsNullOrEmpty(integ.ConfigJson) && integ.ConfigJson.Contains("refresh_token"),
            zoomResponse = body[..Math.Min(1000, body.Length)]
        });
    }

    // ── POST /api/zoom/sync ──────────────────────────────────────────────
    // Manually triggers a Zoom calendar sync for the current user.
    [HttpPost("sync")]
    public async Task<IActionResult> SyncMeetings()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        try
        {
            await _zoomSync.FetchAndSyncMeetingsAsync(userId.Value);
            return Ok(new { message = "Sync complete." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual Zoom sync failed for user {UserId}", userId);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── POST /api/zoom/webhook ───────────────────────────────────────────
    // Zoom calls this endpoint for real-time meeting events.
    // No [Authorize] — Zoom cannot send a JWT; request is verified by HMAC-SHA256 signature.
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> ZoomWebhook()
    {
        // Buffer the body so we can read it twice (signature check + event handling)
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        var timestamp = Request.Headers["x-zm-request-timestamp"].ToString();
        var signature = Request.Headers["x-zm-signature"].ToString();

        // Zoom URL-validation challenge arrives before a real secret is configured
        if (!string.IsNullOrEmpty(body) && body.Contains("endpoint.url_validation"))
        {
            return await HandleUrlValidationAsync(body);
        }

        if (!VerifyWebhookSignature(timestamp, body, signature))
        {
            _logger.LogWarning("Zoom webhook signature invalid — rejected");
            return Unauthorized(new { error = "Invalid signature" });
        }

        try
        {
            using var doc      = JsonDocument.Parse(body);
            var root           = doc.RootElement;
            var eventType      = root.TryGetProperty("event",    out var ev) ? ev.GetString() : null;
            var payload        = root.TryGetProperty("payload",  out var pl) ? pl : default;
            var obj            = payload.ValueKind != JsonValueKind.Undefined &&
                                 payload.TryGetProperty("object", out var ob) ? ob : default;

            _logger.LogInformation("Zoom webhook event: {Event}", eventType);

            switch (eventType)
            {
                case "meeting.created":
                    await HandleMeetingCreatedAsync(payload, obj);
                    break;

                case "meeting.updated":
                    await HandleMeetingUpdatedAsync(obj);
                    break;

                case "meeting.deleted":
                    await HandleMeetingDeletedAsync(obj);
                    break;

                case "meeting.ended":
                    await HandleMeetingEndedAsync(payload, obj);
                    break;

                case "recording.completed":
                    await HandleRecordingCompletedAsync(payload, obj);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zoom webhook processing failed");
            // Return 200 anyway — Zoom retries on non-200 which can flood the endpoint
        }

        return Ok();
    }

    // ── Webhook handlers ─────────────────────────────────────────────────

    private async Task HandleMeetingCreatedAsync(JsonElement payload, JsonElement obj)
    {
        if (obj.ValueKind == JsonValueKind.Undefined) return;

        var userId = await FindUserByZoomOperatorAsync(payload);
        if (userId == null) return;

        var zoomId = GetMeetingId(obj);
        if (string.IsNullOrEmpty(zoomId)) return;

        var existing = await _db.ZoomMeetings
            .FirstOrDefaultAsync(m => m.UserId == userId.Value && m.ZoomMeetingId == zoomId);

        if (existing != null) return; // already synced

        _db.ZoomMeetings.Add(new ZoomMeeting
        {
            Id            = Guid.NewGuid(),
            UserId        = userId.Value,
            ZoomMeetingId = zoomId,
            Topic         = obj.TryGetProperty("topic",    out var t) ? t.GetString() ?? "" : "",
            StartTime     = ParseStartTime(obj),
            Duration      = obj.TryGetProperty("duration", out var d) ? d.GetInt32() : 60,
            JoinUrl       = obj.TryGetProperty("join_url", out var j) ? j.GetString() ?? "" : "",
            Status        = "scheduled",
            IsRecurring   = obj.TryGetProperty("type",     out var ty) &&
                            ty.GetInt32() is 3 or 8,
            CreatedAt     = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        _logger.LogInformation("Webhook: created meeting {Id}", zoomId);
    }

    private async Task HandleMeetingUpdatedAsync(JsonElement obj)
    {
        if (obj.ValueKind == JsonValueKind.Undefined) return;
        var zoomId = GetMeetingId(obj);
        if (string.IsNullOrEmpty(zoomId)) return;

        var meeting = await _db.ZoomMeetings
            .FirstOrDefaultAsync(m => m.ZoomMeetingId == zoomId);
        if (meeting == null) return;

        if (obj.TryGetProperty("topic",    out var t)) meeting.Topic    = t.GetString() ?? meeting.Topic;
        if (obj.TryGetProperty("duration", out var d)) meeting.Duration = d.GetInt32();
        if (obj.TryGetProperty("join_url", out var j)) meeting.JoinUrl  = j.GetString() ?? meeting.JoinUrl;
        var newTime = ParseStartTime(obj);
        if (newTime != default) meeting.StartTime = newTime;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Webhook: updated meeting {Id}", zoomId);
    }

    private async Task HandleMeetingDeletedAsync(JsonElement obj)
    {
        if (obj.ValueKind == JsonValueKind.Undefined) return;
        var zoomId = GetMeetingId(obj);
        if (string.IsNullOrEmpty(zoomId)) return;

        var meeting = await _db.ZoomMeetings
            .FirstOrDefaultAsync(m => m.ZoomMeetingId == zoomId);
        if (meeting == null) return;

        meeting.Status = "cancelled";
        await _db.SaveChangesAsync();
        _logger.LogInformation("Webhook: cancelled meeting {Id}", zoomId);
    }

    private async Task HandleMeetingEndedAsync(JsonElement payload, JsonElement obj)
    {
        if (obj.ValueKind == JsonValueKind.Undefined) return;
        var zoomId = GetMeetingId(obj);
        if (string.IsNullOrEmpty(zoomId)) return;

        var meeting = await _db.ZoomMeetings
            .FirstOrDefaultAsync(m => m.ZoomMeetingId == zoomId);
        if (meeting != null)
        {
            meeting.Status = "ended";
            await _db.SaveChangesAsync();
        }

        // For Basic users: trigger Recall.ai bot + pipeline on meeting.ended
        var userId = await FindUserByZoomOperatorAsync(payload);
        if (userId == null) return;

        var joinUrl = meeting?.JoinUrl ??
            (obj.TryGetProperty("join_url", out var ju) ? ju.GetString() : null);

        if (string.IsNullOrEmpty(joinUrl))
        {
            _logger.LogInformation("Webhook meeting.ended: no join_url — skipping bot join");
            return;
        }

        var topic = meeting?.Topic ??
            (obj.TryGetProperty("topic", out var t) ? t.GetString() : null) ??
            "Zoom Meeting";

        _logger.LogInformation("Webhook meeting.ended — joining for userId={Uid}", userId);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope      = _scopeFactory.CreateScope();
                var recall           = scope.ServiceProvider.GetRequiredService<RecallService>();
                var sessRepo         = scope.ServiceProvider.GetRequiredService<ZoomSessionRepository>();
                var pipelines        = scope.ServiceProvider.GetRequiredService<PipelineRepository>();
                var orchestratorSvc  = scope.ServiceProvider.GetRequiredService<PipelineOrchestrator>();

                var botId   = await recall.CreateBot(joinUrl);
                var session = sessRepo.Create(botId, joinUrl, topic);
                session.UserId = userId.Value;
                await sessRepo.UpdateAsync(session);

                var transcript = await recall.WaitForTranscript(botId);
                if (string.IsNullOrWhiteSpace(transcript)) return;

                var pipeline = pipelines.Create(topic, userId);
                session.PipelineId = pipeline.Id;
                await sessRepo.UpdateAsync(session);
                await orchestratorSvc.RunAsync(pipeline.Id, transcript, topic, botId, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook meeting.ended pipeline failed");
            }
        });
    }

    private async Task HandleRecordingCompletedAsync(JsonElement payload, JsonElement obj)
    {
        // For Pro users: download cloud recording and run pipeline
        var userId = await FindUserByZoomOperatorAsync(payload);
        if (userId == null) return;

        var zoomId = GetMeetingId(obj);
        var topic  = obj.TryGetProperty("topic", out var t) ? t.GetString() : "Zoom Recording";

        _logger.LogInformation(
            "Webhook recording.completed — meetingId={Mid} userId={Uid}", zoomId, userId);

        // Get download URL from recording files
        string? downloadUrl = null;
        if (obj.TryGetProperty("recording_files", out var files))
        {
            foreach (var file in files.EnumerateArray())
            {
                if (file.TryGetProperty("file_type",    out var ft) && ft.GetString() == "MP4" &&
                    file.TryGetProperty("download_url", out var du))
                {
                    downloadUrl = du.GetString();
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            _logger.LogWarning("Recording.completed: no MP4 download URL found");
            return;
        }

        // Zoom download URLs require Bearer token
        var integ = await _db.Integrations
            .FirstOrDefaultAsync(i => i.UserId == userId.Value && i.Type == "Zoom");
        var zoomToken = integ?.AccessToken;

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope      = _scopeFactory.CreateScope();
                var chunker          = scope.ServiceProvider.GetRequiredService<AudioChunkingService>();
                var pipelines        = scope.ServiceProvider.GetRequiredService<PipelineRepository>();
                var orchestratorSvc  = scope.ServiceProvider.GetRequiredService<PipelineOrchestrator>();

                var transcript = await chunker.ChunkAndTranscribeAsync(
                    downloadUrl, "mp4", zoomToken: zoomToken);

                if (string.IsNullOrWhiteSpace(transcript)) return;

                var pipeline = pipelines.Create(topic ?? "Recording", userId);
                await orchestratorSvc.RunAsync(
                    pipeline.Id, transcript, topic ?? "Recording", botId: null, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook recording.completed pipeline failed");
            }
        });
    }

    // ── Webhook helpers ───────────────────────────────────────────────────

    private async Task<IActionResult> HandleUrlValidationAsync(string body)
    {
        try
        {
            using var doc    = JsonDocument.Parse(body);
            var plainToken   = doc.RootElement
                .GetProperty("payload").GetProperty("plainToken").GetString() ?? "";

            var secret = _config["Zoom:WebhookSecret"] ?? "";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(plainToken));
            var encryptedToken = Convert.ToHexString(hash).ToLower();

            return Ok(new { plainToken, encryptedToken });
        }
        catch
        {
            return Ok(); // Don't reject validation challenges
        }
    }

    private bool VerifyWebhookSignature(string timestamp, string body, string signature)
    {
        var secret = _config["Zoom:WebhookSecret"];
        if (string.IsNullOrEmpty(secret)) return true; // Skip if not configured

        var message = $"v0:{timestamp}:{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var expected = "v0=" + Convert.ToHexString(hash).ToLower();

        return string.Equals(expected, signature, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<Guid?> FindUserByZoomOperatorAsync(JsonElement payload)
    {
        // payload.operator_id = Zoom user ID of the event creator
        // payload.object.host_id = meeting host's Zoom user ID
        string? zoomUserId = null;

        if (payload.ValueKind != JsonValueKind.Undefined)
        {
            if (payload.TryGetProperty("operator_id", out var op))
                zoomUserId = op.GetString();

            if (string.IsNullOrEmpty(zoomUserId) &&
                payload.TryGetProperty("object", out var obj) &&
                obj.TryGetProperty("host_id", out var hid))
                zoomUserId = hid.GetString();
        }

        if (string.IsNullOrEmpty(zoomUserId)) return null;

        var integ = await _db.Integrations
            .FirstOrDefaultAsync(i => i.Type == "Zoom" && i.Owner == zoomUserId);

        return integ?.UserId;
    }

    private static string? GetMeetingId(JsonElement obj)
    {
        if (obj.TryGetProperty("id", out var id))
            return id.ValueKind == JsonValueKind.Number
                ? id.GetInt64().ToString()
                : id.GetString();
        return null;
    }

    private static DateTime ParseStartTime(JsonElement obj)
    {
        if (obj.TryGetProperty("start_time", out var st) &&
            st.ValueKind == JsonValueKind.String)
        {
            if (DateTime.TryParse(st.GetString(), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt.ToUniversalTime();
        }
        return default;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public record CreateMeetingRequest(string Topic, DateTime StartTime, int Duration = 30);
