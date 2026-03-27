using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models;
using MOMBotPro.API.Models.Entities;

namespace MOMBotPro.API.Services;

public class PipelineRepository
{
    private readonly IServiceScopeFactory _scopeFactory;

    private static readonly Dictionary<string, int> StepOrder = new()
    {
        ["Transcribe Audio"]          = 0,
        ["Extract Bug from MOM"]      = 1,
        ["Create Jira Ticket"]        = 2,
        ["Scan Codebase"]             = 3,
        ["Generate Fix"]              = 4,
        ["Create Branch & Raise PR"]  = 5,
    };

    public PipelineRepository(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // ── Create ───────────────────────────────────────────────────────────
    public Pipeline Create(string clientName, Guid? userId = null)
    {
        var pipeline = new Pipeline
        {
            ClientName = clientName,
            UserId     = userId,
            Steps = new List<PipelineStep>
            {
                new() { Name = "Transcribe Audio" },
                new() { Name = "Extract Bug from MOM" },
                new() { Name = "Create Jira Ticket" },
                new() { Name = "Scan Codebase" },
                new() { Name = "Generate Fix" },
                new() { Name = "Create Branch & Raise PR" },
            }
        };

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entity = ToEntity(pipeline);
        db.Pipelines.Add(entity);
        db.SaveChanges();

        return pipeline;
    }

    // ── Get ──────────────────────────────────────────────────────────────
    public Pipeline? Get(string id)
    {
        if (!Guid.TryParse(id, out var guid)) return null;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entity = db.Pipelines
            .Include(p => p.Steps)
            .Include(p => p.JiraTicket)
            .Include(p => p.BugAnalysis)
            .Include(p => p.GitHubResult)
            .AsNoTracking()
            .FirstOrDefault(p => p.Id == guid);

        return entity == null ? null : ToModel(entity);
    }

    // ── GetAll ───────────────────────────────────────────────────────────
    public List<Pipeline> GetAll()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        return db.Pipelines
            .Include(p => p.Steps)
            .Include(p => p.JiraTicket)
            .Include(p => p.BugAnalysis)
            .Include(p => p.GitHubResult)
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .AsEnumerable()
            .Select(ToModel)
            .ToList();
    }

    // ── UpdateStep — updates a single step's status + message ────────────
    public void UpdateStep(string pipelineId, string stepName, StepStatus status, string? message = null)
    {
        if (!Guid.TryParse(pipelineId, out var guid)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var step = db.PipelineSteps
            .FirstOrDefault(s => s.PipelineId == guid && s.StepName == stepName);
        if (step == null) return;

        step.Status  = status.ToString();
        step.Message = message;
        if (status == StepStatus.Done || status == StepStatus.Failed)
            step.CompletedAt = DateTime.UtcNow;

        db.SaveChanges();
    }

    // ── Save — persists all pipeline fields + navigation properties ──────
    public void Save(Pipeline pipeline)
    {
        if (!Guid.TryParse(pipeline.Id, out var guid)) return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var entity = db.Pipelines
            .Include(p => p.Steps)
            .Include(p => p.JiraTicket)
            .Include(p => p.BugAnalysis)
            .Include(p => p.GitHubResult)
            .FirstOrDefault(p => p.Id == guid);
        if (entity == null) return;

        // ── Scalar fields ────────────────────────────────────────────────
        entity.Status               = pipeline.Status.ToString();
        entity.Transcript           = pipeline.Transcript;
        entity.BugSummary           = pipeline.BugSummary;
        entity.MOMSummary           = pipeline.MOMSummary;
        entity.CompletedAt          = pipeline.Status is PipelineStatus.Done or PipelineStatus.Failed
                                      ? DateTime.UtcNow : null;
        entity.RecordingVideoUrl    = pipeline.RecordingVideoUrl;
        entity.RecordingAudioUrl    = pipeline.RecordingAudioUrl;
        entity.RecordingExpiresAt   = pipeline.RecordingExpiresAt;
        entity.RecordingTranscriptJson = pipeline.RecordingTranscriptJson;
        entity.BotId                = pipeline.BotId;

        // ── Steps ────────────────────────────────────────────────────────
        foreach (var step in pipeline.Steps)
        {
            var stepEntity = entity.Steps.FirstOrDefault(s => s.StepName == step.Name);
            if (stepEntity != null)
            {
                stepEntity.Status      = step.Status.ToString();
                stepEntity.Message     = step.Message;
                stepEntity.CompletedAt = step.CompletedAt;
            }
        }

        // ── Jira ticket ──────────────────────────────────────────────────
        if (pipeline.JiraTicket != null)
        {
            if (entity.JiraTicket == null)
            {
                entity.JiraTicket = new JiraTicketEntity
                {
                    PipelineId  = guid,
                    TicketKey   = pipeline.JiraTicket.Key,
                    Summary     = pipeline.JiraTicket.Summary,
                    Description = pipeline.JiraTicket.Description,
                    Priority    = pipeline.JiraTicket.Priority,
                    Status      = pipeline.JiraTicket.Status,
                    TicketUrl   = pipeline.JiraTicket.Url,
                    CreatedAt   = pipeline.JiraTicket.Created,
                };
            }
            else
            {
                entity.JiraTicket.TicketKey   = pipeline.JiraTicket.Key;
                entity.JiraTicket.Summary     = pipeline.JiraTicket.Summary;
                entity.JiraTicket.Description = pipeline.JiraTicket.Description;
                entity.JiraTicket.Priority    = pipeline.JiraTicket.Priority;
                entity.JiraTicket.Status      = pipeline.JiraTicket.Status;
                entity.JiraTicket.TicketUrl   = pipeline.JiraTicket.Url;
            }
        }

        // ── Bug analysis ─────────────────────────────────────────────────
        if (pipeline.BugAnalysis != null)
        {
            if (entity.BugAnalysis == null)
            {
                entity.BugAnalysis = new BugAnalysisEntity
                {
                    PipelineId     = guid,
                    FileName       = pipeline.BugAnalysis.FileName,
                    LineNumber     = pipeline.BugAnalysis.LineNumber,
                    BugDescription = pipeline.BugAnalysis.BugDescription,
                    RootCause      = pipeline.BugAnalysis.RootCause,
                    SuggestedFix   = pipeline.BugAnalysis.SuggestedFix,
                    OriginalCode   = pipeline.BugAnalysis.OriginalCode,
                    FixedCode      = pipeline.BugAnalysis.FixedCode,
                };
            }
            else
            {
                entity.BugAnalysis.FileName       = pipeline.BugAnalysis.FileName;
                entity.BugAnalysis.LineNumber     = pipeline.BugAnalysis.LineNumber;
                entity.BugAnalysis.BugDescription = pipeline.BugAnalysis.BugDescription;
                entity.BugAnalysis.RootCause      = pipeline.BugAnalysis.RootCause;
                entity.BugAnalysis.SuggestedFix   = pipeline.BugAnalysis.SuggestedFix;
                entity.BugAnalysis.OriginalCode   = pipeline.BugAnalysis.OriginalCode;
                entity.BugAnalysis.FixedCode      = pipeline.BugAnalysis.FixedCode;
            }
        }

        // ── GitHub result ─────────────────────────────────────────────────
        if (pipeline.GitHubResult != null)
        {
            if (entity.GitHubResult == null)
            {
                entity.GitHubResult = new GitHubResultEntity
                {
                    PipelineId    = guid,
                    BranchName    = pipeline.GitHubResult.BranchName,
                    PRTitle       = pipeline.GitHubResult.PRTitle,
                    PRDescription = pipeline.GitHubResult.PRDescription,
                    PRUrl         = pipeline.GitHubResult.PRUrl,
                    PRNumber      = pipeline.GitHubResult.PRNumber,
                    Status        = pipeline.GitHubResult.Status,
                };
            }
            else
            {
                entity.GitHubResult.BranchName    = pipeline.GitHubResult.BranchName;
                entity.GitHubResult.PRTitle       = pipeline.GitHubResult.PRTitle;
                entity.GitHubResult.PRDescription = pipeline.GitHubResult.PRDescription;
                entity.GitHubResult.PRUrl         = pipeline.GitHubResult.PRUrl;
                entity.GitHubResult.PRNumber      = pipeline.GitHubResult.PRNumber;
                entity.GitHubResult.Status        = pipeline.GitHubResult.Status;
            }
        }

        db.SaveChanges();
    }

    // ── ToEntity (for initial Create only) ───────────────────────────────
    private static PipelineEntity ToEntity(Pipeline m)
    {
        var guid = Guid.TryParse(m.Id, out var g) ? g : Guid.NewGuid();
        var entity = new PipelineEntity
        {
            Id         = guid,
            UserId     = m.UserId,
            ClientName = m.ClientName,
            Status     = m.Status.ToString(),
            CreatedAt  = m.CreatedAt,
        };
        foreach (var step in m.Steps)
        {
            entity.Steps.Add(new PipelineStepEntity
            {
                PipelineId = guid,
                StepName   = step.Name,
                Status     = step.Status.ToString(),
                Message    = step.Message,
                CompletedAt = step.CompletedAt,
            });
        }
        return entity;
    }

    // ── ToModel ───────────────────────────────────────────────────────────
    private static Pipeline ToModel(PipelineEntity e) => new()
    {
        Id          = e.Id.ToString(),
        UserId      = e.UserId,
        ClientName  = e.ClientName,
        Status      = Enum.TryParse<PipelineStatus>(e.Status, out var st) ? st : PipelineStatus.Pending,
        CreatedAt   = e.CreatedAt,
        Transcript  = e.Transcript,
        BugSummary  = e.BugSummary,
        MOMSummary  = e.MOMSummary,
        RecordingVideoUrl      = e.RecordingVideoUrl,
        RecordingAudioUrl      = e.RecordingAudioUrl,
        RecordingExpiresAt     = e.RecordingExpiresAt,
        RecordingTranscriptJson = e.RecordingTranscriptJson,
        BotId       = e.BotId,
        Steps = e.Steps
            .OrderBy(s => StepOrder.TryGetValue(s.StepName, out var ord) ? ord : 99)
            .Select(s => new PipelineStep
            {
                Name        = s.StepName,
                Status      = Enum.TryParse<StepStatus>(s.Status, out var ss) ? ss : StepStatus.Waiting,
                Message     = s.Message,
                CompletedAt = s.CompletedAt,
            })
            .ToList(),
        JiraTicket = e.JiraTicket == null ? null : new JiraTicket
        {
            Key         = e.JiraTicket.TicketKey,
            Summary     = e.JiraTicket.Summary,
            Description = e.JiraTicket.Description,
            Priority    = e.JiraTicket.Priority,
            Status      = e.JiraTicket.Status,
            Url         = e.JiraTicket.TicketUrl,
            Created     = e.JiraTicket.CreatedAt,
        },
        BugAnalysis = e.BugAnalysis == null ? null : new BugAnalysis
        {
            FileName       = e.BugAnalysis.FileName,
            LineNumber     = e.BugAnalysis.LineNumber,
            BugDescription = e.BugAnalysis.BugDescription,
            RootCause      = e.BugAnalysis.RootCause,
            SuggestedFix   = e.BugAnalysis.SuggestedFix,
            OriginalCode   = e.BugAnalysis.OriginalCode,
            FixedCode      = e.BugAnalysis.FixedCode,
        },
        GitHubResult = e.GitHubResult == null ? null : new GitHubResult
        {
            BranchName    = e.GitHubResult.BranchName,
            PRTitle       = e.GitHubResult.PRTitle,
            PRDescription = e.GitHubResult.PRDescription,
            PRUrl         = e.GitHubResult.PRUrl,
            PRNumber      = e.GitHubResult.PRNumber,
            Status        = e.GitHubResult.Status,
        },
    };
}
