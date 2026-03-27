namespace MOMBotPro.API.Models.Entities;

public class ZoomSessionEntity
{
    public Guid     Id            { get; set; } = Guid.NewGuid();
    public Guid?    UserId        { get; set; }
    public string   BotId         { get; set; } = "";
    public string   MeetingUrl    { get; set; } = "";
    public string   ClientName    { get; set; } = "";
    public string   Status        { get; set; } = "Joining";
    public string?  StatusMessage { get; set; }
    public Guid?    PipelineId    { get; set; }
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
}
