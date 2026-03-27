namespace MOMBotPro.API.Models;

// ── Entity ────────────────────────────────────────────────
public class AppUser
{
    public Guid   Id                      { get; set; } = Guid.NewGuid();
    public string FullName                { get; set; } = "";
    public string Email                   { get; set; } = "";
    public string PasswordHash            { get; set; } = "";
    public string CompanyName             { get; set; } = "";
    public string Domain                  { get; set; } = "";
    public string Role                    { get; set; } = "User";   // Admin | User
    public string SubscriptionPlan        { get; set; } = "free_trial";
    public int    FreeTrialMeetingsLeft   { get; set; } = 3;
    public bool   IsActive                { get; set; } = true;
    public DateTime CreatedAt             { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt             { get; set; } = DateTime.UtcNow;
}

// ── Integration entity ────────────────────────────────────
public class Integration
{
    public Guid      Id          { get; set; } = Guid.NewGuid();
    public Guid      UserId      { get; set; }
    public string    Type        { get; set; } = "";  // Jira|GitHub|Slack|Gmail
    public string?   ConfigJson  { get; set; }        // kept for backward compat
    public bool      IsConnected { get; set; }
    public DateTime? ConnectedAt { get; set; }

    // ── Explicit credential fields (populated by IntegrationController) ──
    public string? Domain      { get; set; }   // Jira: atlassian domain / Slack: webhook URL
    public string? Email       { get; set; }   // Jira/Gmail: account email
    public string? AccessToken { get; set; }   // Jira: API token | GitHub: PAT | Gmail: app password
    public string? ProjectKey  { get; set; }   // Jira: project key (e.g. DEV)
    public string? Owner       { get; set; }   // GitHub: repo owner
    public string? Repo        { get; set; }   // GitHub: repo name
    public string?   AccountId  { get; set; }   // Jira: accountId (e.g. 712020:xxx) for reporter
    public DateTime? ExpiresAt  { get; set; }   // UTC expiry of the stored access token (null = unknown)
}

// ── Zoom scheduled join settings ──────────────────────────
public class ZoomSettings
{
    public Guid      Id            { get; set; } = Guid.NewGuid();
    public Guid      UserId        { get; set; }
    public string    ZoomLink      { get; set; } = "";
    public bool      IsRecurring   { get; set; }
    public TimeOnly? ScheduledTime { get; set; }          // e.g. 10:00
    public string?   ScheduledDays { get; set; }          // "Mon,Tue,Wed,Thu,Fri"
    public bool      IsActive      { get; set; } = true;
    public DateTime  CreatedAt     { get; set; } = DateTime.UtcNow;
}

// ── OAuth state (CSRF protection) ─────────────────────────
public class OAuthState
{
    public Guid     Id           { get; set; } = Guid.NewGuid();
    public string   State        { get; set; } = "";   // cryptographically random token
    public string   Provider     { get; set; } = "";   // github|jira|gmail|slack
    public Guid     UserId       { get; set; }
    public string?  CodeVerifier { get; set; }         // PKCE verifier (null for Slack)
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt    { get; set; }
}

// ── Request / response DTOs ───────────────────────────────
public class RegisterRequest
{
    public string FullName        { get; set; } = "";
    public string CompanyName     { get; set; } = "";
    public string Domain          { get; set; } = "";
    public string Email           { get; set; } = "";
    public string Password        { get; set; } = "";
    public string ConfirmPassword { get; set; } = "";
}

public class LoginRequest
{
    public string Email    { get; set; } = "";
    public string Password { get; set; } = "";
}

public class AuthResponse
{
    public string Token  { get; set; } = "";
    public AppUser User  { get; set; } = new();
}

public class ConnectIntegrationRequest
{
    public string Type       { get; set; } = "";
    public object ConfigJson { get; set; } = new();
}

public class UpgradeRequest
{
    public string PlanId { get; set; } = "";
}

// ── Synced Zoom calendar meeting ──────────────────────────
public class ZoomMeeting
{
    public Guid     Id            { get; set; } = Guid.NewGuid();
    public Guid     UserId        { get; set; }
    public string   ZoomMeetingId { get; set; } = "";   // Zoom's numeric meeting ID
    public string   Topic         { get; set; } = "";
    public DateTime StartTime     { get; set; }
    public int      Duration      { get; set; }          // minutes
    public string   JoinUrl       { get; set; } = "";
    public string   Status        { get; set; } = "scheduled"; // scheduled|bot_joining|ended|cancelled
    public bool     IsRecurring   { get; set; }
    public DateTime CreatedAt     { get; set; } = DateTime.UtcNow;
}

public class SaveIntegrationRequest
{
    public string  Type        { get; set; } = "";
    public string? Domain      { get; set; }   // Jira: base URL
    public string? Email       { get; set; }   // Jira/Gmail: account email
    public string? ApiToken    { get; set; }   // Jira: API token
    public string? ProjectKey  { get; set; }   // Jira: project key
    public string? WebhookUrl  { get; set; }   // Slack: incoming webhook
    public string? ChannelName { get; set; }   // Slack: channel name
    public string? Token       { get; set; }   // GitHub: personal access token
    public string? Owner       { get; set; }   // GitHub: repo owner
    public string? Repo        { get; set; }   // GitHub: repo name
    public string? FromEmail   { get; set; }   // Gmail: from address
    public string? AppPassword { get; set; }   // Gmail: app password
    public string? ToEmail     { get; set; }   // Gmail: default recipient
}
