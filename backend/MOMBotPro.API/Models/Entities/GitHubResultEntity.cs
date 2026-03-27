namespace MOMBotPro.API.Models.Entities;

public class GitHubResultEntity
{
    public Guid   Id            { get; set; } = Guid.NewGuid();
    public Guid   PipelineId    { get; set; }
    public string BranchName    { get; set; } = "";
    public string PRTitle       { get; set; } = "";
    public string PRDescription { get; set; } = "";
    public string PRUrl         { get; set; } = "";
    public string PRNumber      { get; set; } = "";
    public string Status        { get; set; } = "Open";

    // Navigation
    public PipelineEntity Pipeline { get; set; } = null!;
}
