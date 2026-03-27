namespace MOMBotPro.API.Models.Entities;

public class PipelineEntity
{
    public Guid     Id              { get; set; } = Guid.NewGuid();
    public Guid?    UserId          { get; set; }
    public string   ClientName      { get; set; } = "";
    public string   Status          { get; set; } = "Pending";
    public string?  Transcript      { get; set; }
    public string?  BugSummary      { get; set; }
    public string?  MOMSummary      { get; set; }
    public bool     UsedMcp         { get; set; } = false;
    public bool     IsArchived      { get; set; } = false;
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt    { get; set; }
    public int?     DurationMinutes { get; set; }

    // Recording (from Recall.ai)
    public string?   RecordingVideoUrl      { get; set; }
    public string?   RecordingAudioUrl      { get; set; }
    public DateTime? RecordingExpiresAt     { get; set; }
    public string?   RecordingTranscriptJson { get; set; }
    public string?   BotId                  { get; set; }

    // Navigation properties
    public ICollection<PipelineStepEntity>  Steps      { get; set; } = new List<PipelineStepEntity>();
    public JiraTicketEntity?                JiraTicket { get; set; }
    public BugAnalysisEntity?               BugAnalysis{ get; set; }
    public GitHubResultEntity?              GitHubResult { get; set; }
}
