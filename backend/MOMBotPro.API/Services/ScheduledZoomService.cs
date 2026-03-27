using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models.Entities;

namespace MOMBotPro.API.Services;

/// <summary>
/// Background service that checks every minute for ZoomMeetings whose StartTime
/// is within the next 2 minutes, then auto-joins via Recall.ai.
/// Replaces the old ZoomSettings-based scheduler.
/// </summary>
public class ScheduledZoomService : BackgroundService
{
    private readonly IServiceScopeFactory          _scopeFactory;
    private readonly ILogger<ScheduledZoomService> _logger;

    public ScheduledZoomService(
        IServiceScopeFactory          scopeFactory,
        ILogger<ScheduledZoomService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledZoomService started.");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            try   { await CheckAndJoinAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ScheduledZoomService tick failed");
            }
        }

        _logger.LogInformation("ScheduledZoomService stopped.");
    }

    private async Task CheckAndJoinAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recall      = scope.ServiceProvider.GetRequiredService<RecallService>();

        var utcNow     = DateTime.UtcNow;
        var windowEnd  = utcNow.AddMinutes(2);
        // Allow 1-minute lookback so meetings exactly on the minute aren't missed
        var windowStart = utcNow.AddMinutes(-1);

        // Find synced meetings starting within the 2-minute window
        var meetings = await db.ZoomMeetings
            .Where(m => m.StartTime >= windowStart &&
                        m.StartTime <= windowEnd   &&
                        m.Status    == "scheduled")
            .ToListAsync(ct);

        if (meetings.Count == 0) return;

        _logger.LogInformation(
            "ScheduledZoomService: {Count} meeting(s) starting within 2 min",
            meetings.Count);

        var tenMinAgo = utcNow.AddMinutes(-10);

        foreach (var meeting in meetings)
        {
            try
            {
                // Verify the user still has an active Zoom integration
                var hasInteg = await db.Integrations.AnyAsync(i =>
                    i.UserId == meeting.UserId &&
                    i.Type   == "Zoom"         &&
                    i.IsConnected, ct);

                if (!hasInteg)
                {
                    _logger.LogInformation(
                        "Skipping meetingId={Mid} — no active Zoom integration for userId={Uid}",
                        meeting.ZoomMeetingId, meeting.UserId);
                    continue;
                }

                // Dedup: skip if a ZoomSession was already created in the last 10 minutes
                var recentExists = await db.ZoomSessions
                    .AnyAsync(z => z.UserId == meeting.UserId &&
                                   z.CreatedAt > tenMinAgo, ct);

                if (recentExists)
                {
                    _logger.LogInformation(
                        "Skipping meetingId={Mid} for userId={Uid} — recent session exists",
                        meeting.ZoomMeetingId, meeting.UserId);
                    continue;
                }

                _logger.LogInformation(
                    "Auto-joining meeting={Topic} (id={Mid}) for userId={Uid}",
                    meeting.Topic, meeting.ZoomMeetingId, meeting.UserId);

                // Mark immediately to prevent duplicate joins on the next tick
                meeting.Status = "bot_joining";
                await db.SaveChangesAsync(ct);

                // Ask Recall.ai to join the meeting
                var botId = await recall.CreateBot(meeting.JoinUrl);

                var session = new ZoomSessionEntity
                {
                    Id            = Guid.NewGuid(),
                    BotId         = botId,
                    MeetingUrl    = meeting.JoinUrl,
                    ClientName    = meeting.Topic,
                    Status        = "Joining",
                    StatusMessage = $"Auto-joining: {meeting.Topic}",
                    UserId        = meeting.UserId,
                    CreatedAt     = DateTime.UtcNow,
                };

                db.ZoomSessions.Add(session);
                await db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Bot created: sessionId={Sid} botId={BotId} meeting={Mid}",
                    session.Id, botId, meeting.ZoomMeetingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to auto-join meetingId={Mid} for userId={Uid}",
                    meeting.ZoomMeetingId, meeting.UserId);

                // Reset status so it can be retried next tick
                meeting.Status = "scheduled";
                try { await db.SaveChangesAsync(ct); } catch { }
            }
        }
    }
}
