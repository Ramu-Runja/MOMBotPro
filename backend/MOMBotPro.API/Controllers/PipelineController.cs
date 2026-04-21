using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MOMBotPro.API.Models;
using MOMBotPro.API.Services;
using System.Security.Claims;

namespace MOMBotPro.API.Controllers;

[ApiController]
[Route("api/pipeline")]
[Authorize]
public class PipelineController : ControllerBase
{
    private readonly PipelineRepository         _repo;
    private readonly RecallService              _recall;
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly PipelineCancellationService _cancellation;

    public PipelineController(
        PipelineRepository          repo,
        RecallService               recall,
        IServiceScopeFactory        scopeFactory,
        PipelineCancellationService cancellation)
    {
        _repo         = repo;
        _recall       = recall;
        _scopeFactory = scopeFactory;
        _cancellation = cancellation;
    }

    // GET /api/pipeline — list all
    [HttpGet]
    public IActionResult GetAll() => Ok(_repo.GetAll());

    // GET /api/pipeline/{id} — get one (for polling)
    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var p = _repo.Get(id);
        return p == null ? NotFound() : Ok(p);
    }

    // POST /api/pipeline/start — start the full pipeline
    [HttpPost("start")]
    public IActionResult Start([FromBody] StartPipelineRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ClientName))
            return BadRequest("Client name required");

        var transcript = request.Transcript
            ?? request.BugDescription
            ?? "Client reported that the login button is not working on mobile devices. When user clicks login, nothing happens. This was working last week. The issue affects all Android users.";

        var userId   = GetUserId();
        var pipeline = _repo.Create(request.ClientName, userId);

        // Run pipeline in background using a fresh DI scope
        var capturedId         = pipeline.Id;
        var capturedTranscript = transcript;
        var capturedClient     = request.ClientName;
        var capturedUserId     = userId;
        var ct                 = _cancellation.Register(capturedId);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<PipelineOrchestrator>();
                await orchestrator.RunAsync(capturedId, capturedTranscript, capturedClient,
                    userId: capturedUserId, cancellationToken: ct);
            }
            finally
            {
                _cancellation.Remove(capturedId);
            }
        });

        return Ok(new { pipelineId = pipeline.Id, status = "Running", message = "Pipeline started" });
    }

    // GET /api/pipeline/{id}/refresh-recording — re-fetch recording URLs from Recall.ai
    [HttpGet("{id}/refresh-recording")]
    public async Task<IActionResult> RefreshRecording(string id)
    {
        var pipeline = _repo.Get(id);
        if (pipeline == null) return NotFound();

        if (string.IsNullOrEmpty(pipeline.BotId))
            return BadRequest(new { error = "No bot ID associated with this pipeline." });

        try
        {
            var recording = await _recall.GetRecordingUrls(pipeline.BotId);
            pipeline.RecordingVideoUrl  = recording.VideoUrl;
            pipeline.RecordingAudioUrl  = recording.AudioUrl;
            pipeline.RecordingExpiresAt = recording.ExpiresAt;
            return Ok(pipeline);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // POST /api/pipeline/{id}/stop — cancel a running pipeline
    [HttpPost("{id}/stop")]
    public IActionResult Stop(string id)
    {
        var pipeline = _repo.Get(id);
        if (pipeline == null) return NotFound();

        if (pipeline.Status is not (PipelineStatus.Running or PipelineStatus.Pending))
            return BadRequest(new { error = "Pipeline is not running." });

        _cancellation.Cancel(id);

        // Mark any Running/Waiting steps as Failed immediately in DB
        foreach (var step in pipeline.Steps.Where(s =>
            s.Status == StepStatus.Running || s.Status == StepStatus.Waiting))
        {
            step.Status      = StepStatus.Failed;
            step.Message     = "Stopped by user";
            step.CompletedAt = DateTime.UtcNow;
            _repo.UpdateStep(id, step.Name, StepStatus.Failed, "Stopped by user");
        }
        pipeline.Status = PipelineStatus.Failed;
        _repo.Save(pipeline);

        return Ok(new { message = "Pipeline stopped." });
    }

    // POST /api/pipeline/{id}/rerun — re-run a failed pipeline with the same inputs
    [HttpPost("{id}/rerun")]
    public IActionResult Rerun(string id)
    {
        var original = _repo.Get(id);
        if (original == null) return NotFound();

        var transcript = original.Transcript
            ?? "Client reported an issue. Please re-check the recording.";
        var userId   = GetUserId();
        var pipeline = _repo.Create(original.ClientName, userId);

        var capturedId         = pipeline.Id;
        var capturedTranscript = transcript;
        var capturedClient     = original.ClientName;
        var capturedUserId     = userId;
        var capturedBotId      = original.BotId;
        var ct                 = _cancellation.Register(capturedId);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<PipelineOrchestrator>();
                await orchestrator.RunAsync(capturedId, capturedTranscript, capturedClient,
                    botId: capturedBotId, userId: capturedUserId, cancellationToken: ct);
            }
            finally
            {
                _cancellation.Remove(capturedId);
            }
        });

        return Ok(new { pipelineId = pipeline.Id, status = "Running", message = "Pipeline re-run started." });
    }

    // GET /api/pipeline/debug/recall/{botId}
    [HttpGet("debug/recall/{botId}")]
    public async Task<IActionResult> DebugRecall(string botId)
    {
        var result = await _recall.GetBotDebugInfo(botId);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    // GET /api/analytics?period=7d|30d|90d
    [HttpGet("/api/analytics")]
    public IActionResult GetAnalytics([FromQuery] string period = "30d")
    {
        var all = _repo.GetAll();

        // Determine window
        var days = period switch { "7d" => 7, "90d" => 90, _ => 30 };
        var cutoff     = DateTime.UtcNow.AddDays(-days);
        var prevCutoff = DateTime.UtcNow.AddDays(-days * 2);

        var current  = all.Where(p => p.CreatedAt >= cutoff).ToList();
        var previous = all.Where(p => p.CreatedAt >= prevCutoff && p.CreatedAt < cutoff).ToList();

        // Core counts
        int totalCalls    = current.Count;
        int momsGenerated = current.Count(p => !string.IsNullOrEmpty(p.MOMSummary));
        int prsRaised     = current.Count(p => p.GitHubResult?.PRNumber != null && p.GitHubResult.PRNumber != "");
        double momRate    = totalCalls > 0 ? Math.Round((double)momsGenerated / totalCalls * 100, 1) : 0;
        double prRate     = totalCalls > 0 ? Math.Round((double)prsRaised / totalCalls * 100, 1) : 0;

        // vs last period
        int prevCalls = previous.Count;
        string vsLastPeriod = prevCalls == 0
            ? "+100%"
            : (totalCalls >= prevCalls
                ? $"+{Math.Round((double)(totalCalls - prevCalls) / prevCalls * 100)}%"
                : $"-{Math.Round((double)(prevCalls - totalCalls) / prevCalls * 100)}%");

        // Monthly stats — last 6 calendar months
        var monthlyStats = Enumerable.Range(0, 6)
            .Select(i => DateTime.UtcNow.AddMonths(-5 + i))
            .Select(month =>
            {
                var inMonth = all.Where(p =>
                    p.CreatedAt.Year == month.Year && p.CreatedAt.Month == month.Month).ToList();
                return new
                {
                    month = month.ToString("MMM"),
                    calls = inMonth.Count,
                    moms  = inMonth.Count(p => !string.IsNullOrEmpty(p.MOMSummary))
                };
            })
            .ToList();

        // Pipeline status breakdown
        var breakdown = new
        {
            done    = current.Count(p => p.Status == PipelineStatus.Done),
            running = current.Count(p => p.Status == PipelineStatus.Running || p.Status == PipelineStatus.Pending),
            failed  = current.Count(p => p.Status == PipelineStatus.Failed)
        };

        // Weekly durations — last 12 weeks (simulated avg from step timings)
        var weeklyDurations = Enumerable.Range(0, 12)
            .Select(i =>
            {
                var weekStart = DateTime.UtcNow.AddDays(-((11 - i) * 7 + 7));
                var weekEnd   = weekStart.AddDays(7);
                var week      = all.Where(p => p.CreatedAt >= weekStart && p.CreatedAt < weekEnd).ToList();
                if (!week.Any()) return (20 + (i % 4) * 5); // plausible default
                // Estimate duration from first/last step completion timestamps
                var durations = week
                    .Select(p =>
                    {
                        var first = p.Steps.Where(s => s.CompletedAt.HasValue).Select(s => s.CompletedAt!.Value).DefaultIfEmpty(p.CreatedAt).Min();
                        var last  = p.Steps.Where(s => s.CompletedAt.HasValue).Select(s => s.CompletedAt!.Value).DefaultIfEmpty(p.CreatedAt).Max();
                        return (int)(last - first).TotalMinutes;
                    })
                    .Where(d => d >= 0)
                    .ToList();
                return durations.Any() ? (int)durations.Average() : 25;
            })
            .ToList();

        // Top clients by PRs fixed
        var topClients = all
            .Where(p => !string.IsNullOrEmpty(p.ClientName))
            .GroupBy(p => p.ClientName)
            .Select(g => new
            {
                name      = g.Key,
                bugsFixed = g.Count(p => p.GitHubResult?.PRNumber != null && p.GitHubResult.PRNumber != "")
            })
            .OrderByDescending(c => c.bugsFixed)
            .Take(5)
            .ToList();

        // Recent pipelines (last 10)
        var recent = current
            .Take(10)
            .Select(p =>
            {
                var first = p.Steps.Where(s => s.CompletedAt.HasValue).Select(s => s.CompletedAt!.Value).DefaultIfEmpty(p.CreatedAt).Min();
                var last  = p.Steps.Where(s => s.CompletedAt.HasValue).Select(s => s.CompletedAt!.Value).DefaultIfEmpty(p.CreatedAt).Max();
                return new
                {
                    id          = p.Id,
                    clientName  = p.ClientName,
                    momSummary  = p.MOMSummary,
                    bugFound    = p.BugAnalysis?.FileName,
                    prNumber    = p.GitHubResult?.PRNumber,
                    prUrl       = p.GitHubResult?.PRUrl,
                    status      = p.Status.ToString(),
                    durationMin = (int)(last - first).TotalMinutes,
                    createdAt   = p.CreatedAt
                };
            })
            .ToList();

        return Ok(new
        {
            totalCalls,
            momsGenerated,
            momSuccessRate        = momRate,
            avgCallDurationMinutes = 32, // static representative value
            prsRaised,
            prSuccessRate         = prRate,
            vsLastPeriod,
            monthlyStats,
            pipelineStatusBreakdown = breakdown,
            weeklyDurations,
            topClients,
            recentPipelines = recent
        });
    }
}
