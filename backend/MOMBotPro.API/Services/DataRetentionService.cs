using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Data;

namespace MOMBotPro.API.Services;

/// <summary>
/// Nightly background service (runs at 02:00 UTC) that:
/// - Deletes mombot_* temp directories older than 30 days.
/// - Soft-deletes Pipeline records older than 90 days (sets IsArchived = true).
/// </summary>
public class DataRetentionService : BackgroundService
{
    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly ILogger<DataRetentionService>   _logger;

    public DataRetentionService(
        IServiceScopeFactory          scopeFactory,
        ILogger<DataRetentionService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitUntil2AmUtcAsync(stoppingToken);
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                await PurgeOldTempDirectoriesAsync();
                await ArchiveOldPipelinesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataRetentionService run failed");
            }
        }
    }

    // ── Temp-directory cleanup ─────────────────────────────────────────────

    private Task PurgeOldTempDirectoriesAsync()
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var tempRoot = Path.GetTempPath();
        var deleted = 0;

        try
        {
            foreach (var dir in Directory.GetDirectories(tempRoot, "mombot_*"))
            {
                try
                {
                    var info = new DirectoryInfo(dir);
                    if (info.CreationTimeUtc < cutoff)
                    {
                        Directory.Delete(dir, recursive: true);
                        deleted++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not delete temp dir {Dir}: {Msg}", dir, ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Temp-dir scan failed: {Msg}", ex.Message);
        }

        _logger.LogInformation(
            "DataRetention: removed {N} mombot_* temp dir(s) older than 30 days", deleted);
        return Task.CompletedTask;
    }

    // ── Pipeline soft-delete ───────────────────────────────────────────────

    private async Task ArchiveOldPipelinesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoff = DateTime.UtcNow.AddDays(-90);
        var stale = await db.Pipelines
            .Where(p => !p.IsArchived && p.CreatedAt < cutoff)
            .ToListAsync(ct);

        if (stale.Count == 0)
        {
            _logger.LogInformation("DataRetention: no pipelines to archive");
            return;
        }

        foreach (var p in stale)
            p.IsArchived = true;

        await db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "DataRetention: archived {N} pipeline(s) older than 90 days", stale.Count);
    }

    // ── Scheduling helper ──────────────────────────────────────────────────

    private static async Task WaitUntil2AmUtcAsync(CancellationToken ct)
    {
        var now  = DateTime.UtcNow;
        var next = new DateTime(now.Year, now.Month, now.Day, 2, 0, 0, DateTimeKind.Utc);
        if (next <= now) next = next.AddDays(1);

        var delay = next - now;
        await Task.Delay(delay, ct);
    }
}
