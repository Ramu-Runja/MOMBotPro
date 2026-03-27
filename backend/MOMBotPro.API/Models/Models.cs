namespace MOMBotPro.API.Models;

// ── PIPELINE ─────────────────────────────────────
public class Pipeline
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Guid? UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string ClientName { get; set; } = "";
    public PipelineStatus Status { get; set; } = PipelineStatus.Pending;
    public List<PipelineStep> Steps { get; set; } = new();

    // Step outputs
    public string? Transcript { get; set; }
    public string? BugSummary { get; set; }
    public string? MOMSummary { get; set; }
    public JiraTicket? JiraTicket { get; set; }
    public BugAnalysis? BugAnalysis { get; set; }
    public GitHubResult? GitHubResult { get; set; }

    // Recording (from Recall.ai)
    public string? RecordingVideoUrl     { get; set; }
    public string? RecordingAudioUrl     { get; set; }
    public DateTime? RecordingExpiresAt  { get; set; }
    public string? RecordingTranscriptJson { get; set; }
    public string? BotId                 { get; set; }
}

public enum PipelineStatus { Pending, Running, Done, Failed }

public class PipelineStep
{
    public string Name { get; set; } = "";
    public StepStatus Status { get; set; } = StepStatus.Waiting;
    public string? Message { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum StepStatus { Waiting, Running, Done, Failed }

// ── JIRA ─────────────────────────────────────────
public class JiraTicket
{
    public string Key { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Description { get; set; } = "";
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public string Reporter { get; set; } = "ramurunja91@gmail.com";
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public string? Url { get; set; }
}

// ── BUG ANALYSIS ─────────────────────────────────
public class BugAnalysis
{
    public string FileName { get; set; } = "";
    public int LineNumber { get; set; }
    public string BugDescription { get; set; } = "";
    public string RootCause { get; set; } = "";
    public string SuggestedFix { get; set; } = "";
    public string OriginalCode { get; set; } = "";
    public string FixedCode { get; set; } = "";
}

// ── GITHUB ───────────────────────────────────────
public class GitHubResult
{
    public string BranchName { get; set; } = "";
    public string PRTitle { get; set; } = "";
    public string PRDescription { get; set; } = "";
    public string PRUrl { get; set; } = "";
    public string PRNumber { get; set; } = "";
    public string Status { get; set; } = "Open";
}

// ── ZOOM SESSION ─────────────────────────────────
public class ZoomSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public Guid?  UserId { get; set; }
    public string BotId { get; set; } = "";
    public string MeetingUrl { get; set; } = "";
    public string ClientName { get; set; } = "";
    public ZoomSessionStatus Status { get; set; } = ZoomSessionStatus.Joining;
    public string StatusMessage { get; set; } = "Bot is joining the Zoom call...";
    public string? Transcript { get; set; }
    public string? PipelineId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum ZoomSessionStatus { Joining, InCall, Recording, Processing, Done, Failed }

// ── REQUESTS ─────────────────────────────────────
public class StartPipelineRequest
{
    public string ClientName { get; set; } = "";
    public string? Transcript { get; set; }
    public string? BugDescription { get; set; }
}

public class JoinZoomRequest
{
    public string ClientName { get; set; } = "";
    public string MeetingUrl { get; set; } = "";
}

// ── RECORDING ────────────────────────────────────
public class RecordingInfo
{
    public string VideoUrl { get; set; } = "";
    public string AudioUrl { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
}
