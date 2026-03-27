namespace MOMBotPro.API.Models.Entities;

public class BugAnalysisEntity
{
    public Guid   Id             { get; set; } = Guid.NewGuid();
    public Guid   PipelineId     { get; set; }
    public string FileName       { get; set; } = "";
    public int    LineNumber     { get; set; }
    public string BugDescription { get; set; } = "";
    public string RootCause      { get; set; } = "";
    public string SuggestedFix   { get; set; } = "";
    public string OriginalCode   { get; set; } = "";
    public string FixedCode      { get; set; } = "";

    // Navigation
    public PipelineEntity Pipeline { get; set; } = null!;
}
