namespace MOMBotPro.API.Models.Entities;

public class JiraTicketEntity
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public Guid     PipelineId  { get; set; }
    public string   TicketKey   { get; set; } = "";
    public string   Summary     { get; set; } = "";
    public string   Description { get; set; } = "";
    public string   Priority    { get; set; } = "Medium";
    public string   Status      { get; set; } = "Open";
    public string?  TicketUrl   { get; set; }
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;

    // Navigation
    public PipelineEntity Pipeline { get; set; } = null!;
}
