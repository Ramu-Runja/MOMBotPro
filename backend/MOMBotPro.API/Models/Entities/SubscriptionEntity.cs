namespace MOMBotPro.API.Models.Entities;

public class SubscriptionEntity
{
    public Guid      Id         { get; set; } = Guid.NewGuid();
    public Guid      UserId     { get; set; }
    public string    Plan       { get; set; } = "free_trial";
    public DateTime  StartDate  { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate    { get; set; }
    public decimal   PriceUSD   { get; set; }
    public decimal   PriceINR   { get; set; }
    public string    Status     { get; set; } = "Active";
    public DateTime  CreatedAt  { get; set; } = DateTime.UtcNow;
}
