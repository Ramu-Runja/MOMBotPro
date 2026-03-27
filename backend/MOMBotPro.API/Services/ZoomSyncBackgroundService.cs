using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Data;

namespace MOMBotPro.API.Services;

/// <summary>
/// Runs every 30 minutes and syncs Zoom calendar meetings for every user
/// that has an active Zoom integration.
/// </summary>
public class ZoomSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly ILogger<ZoomSyncBackgroundService> _logger;

    public ZoomSyncBackgroundService(
        IServiceScopeFactory               scopeFactory,
        ILogger<ZoomSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ZoomSyncBackgroundService started.");

        // Initial delay — let the app fully start before first sync
        await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            try   { await SyncAllUsersAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ZoomSyncBackgroundService tick failed");
            }
        }

        _logger.LogInformation("ZoomSyncBackgroundService stopped.");
    }

    private async Task SyncAllUsersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var syncSvc     = scope.ServiceProvider.GetRequiredService<ZoomSyncService>();

        // Find all users with active Zoom integrations
        var userIds = await db.Integrations
            .Where(i => i.Type == "Zoom" && i.IsConnected)
            .Select(i => i.UserId)
            .ToListAsync(ct);

        _logger.LogInformation(
            "ZoomSyncBackgroundService: syncing {Count} user(s)", userIds.Count);

        foreach (var userId in userIds)
        {
            try
            {
                await syncSvc.FetchAndSyncMeetingsAsync(userId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Zoom sync failed for userId={UserId}", userId);
            }
        }
    }
}
