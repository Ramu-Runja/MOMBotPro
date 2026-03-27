namespace MOMBotPro.API.Models.Entities;

public class PipelineStepEntity
{
    public Guid      Id          { get; set; } = Guid.NewGuid();
    public Guid      PipelineId  { get; set; }
    public string    StepName    { get; set; } = "";
    public string    Status      { get; set; } = "Waiting";
    public string?   Message     { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public PipelineEntity Pipeline { get; set; } = null!;
}
