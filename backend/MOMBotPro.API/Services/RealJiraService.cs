using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Services;

/// <summary>
/// Creates Jira tickets via REST API v3.
/// Credentials are injected at runtime via Configure() — no DB lookup, no mock fallback.
/// OAuth mode: uses Bearer token + api.atlassian.com base URL; auto-refreshes on 401.
/// Basic mode: uses email:apiToken + tenant subdomain URL.
/// </summary>
public class RealJiraService : IJiraService
{
    private readonly IHttpClientFactory       _httpFactory;
    private readonly ITokenProvider           _tokenProvider;
    private readonly ILogger<RealJiraService> _logger;

    private string _domain     = "";
    private string _email      = "";
    private string _apiToken   = "";   // fallback / Basic-auth token
    private string _projectKey = "";
    private string _siteUrl    = "";   // OAuth: https://mysite.atlassian.net
    private Guid?  _userId     = null; // used by TokenProvider for OAuth refresh

    public RealJiraService(
        IHttpClientFactory       httpFactory,
        ITokenProvider           tokenProvider,
        ILogger<RealJiraService> logger)
    {
        _httpFactory   = httpFactory;
        _tokenProvider = tokenProvider;
        _logger        = logger;
    }

    public void Configure(
        string  domain,
        string  email,
        string  apiToken,
        string  projectKey,
        string? accountId  = null,
        Guid?   userId     = null)
    {
        _domain     = domain?.Trim() ?? "";
        _email      = email?.Trim() ?? "";
        _apiToken   = apiToken?.Trim() ?? "";
        _projectKey = projectKey?.Trim() ?? "";
        _siteUrl    = accountId?.TrimEnd('/') ?? "";
        _userId     = userId;

        _logger.LogInformation(
            "Jira configured: {Domain}/{Key}, Email: {Email}, " +
            "Token starts: {Token}",
            _domain, _projectKey, _email,
            _apiToken.Length > 10
                ? _apiToken[..10] + "..."
                : _apiToken);
    }

    public async Task<JiraTicket> CreateTicket(
        string bugSummary,
        string clientName,
        Guid? userId = null)
    {
        if (string.IsNullOrEmpty(_domain) ||
            string.IsNullOrEmpty(_apiToken))
        {
            _logger.LogWarning("Jira not configured — skipping");
            return new JiraTicket
            {
                Key = "NOT-CONFIGURED",
                Summary = bugSummary,
                Status = "Skipped — Jira not connected"
            };
        }

        try
        {
            // OAuth 3LO: email is null/empty OR token is a JWT (starts with "ey")
            // API token: non-empty email + non-JWT token → Basic auth
            bool isOAuth  = string.IsNullOrEmpty(_email) || _apiToken.StartsWith("ey");
            var  url      = isOAuth
                ? $"https://api.atlassian.com/ex/jira/{_domain}/rest/api/3/issue"
                : $"https://{_domain.Replace("https://", "").Replace("http://", "").TrimEnd('/')}/rest/api/3/issue";

            var priority = DetectPriority(bugSummary);
            var summary  = TruncateSummary(bugSummary);

            // ── Payload — strongly typed, NO Dictionary<string,object> ──
            var payload = new
            {
                fields = new
                {
                    project = new { key = _projectKey },
                    summary = bugSummary.Length > 255
                                    ? bugSummary[..255]
                                    : bugSummary,
                    issuetype = new { name = "Task" },
                    priority = new { name = priority },
                    description = new
                    {
                        type = "doc",
                        version = 1,
                        content = new[]
                        {
                            new
                            {
                                type    = "paragraph",
                                content = new[]
                                {
                                    new
                                    {
                                        type = "text",
                                        text = $"Client: {clientName}\n" +
                                               $"Bug: {bugSummary}\n" +
                                               $"Source: MOMBot Pro — " +
                                               $"Auto-generated from meeting"
                                    }
                                }
                            }
                        }
                    }
                    // ❌ NO labels  — next-gen projects reject them
                    // ❌ NO reporter — next-gen projects reject it too
                }
            };

            using var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            void SetAuth(string token) =>
                http.DefaultRequestHeaders.Authorization = isOAuth
                    ? new AuthenticationHeaderValue("Bearer", token)
                    : new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_email}:{_apiToken}")));

            // For OAuth with userId: ensure we have a fresh token before the call
            if (isOAuth && _userId.HasValue)
            {
                try
                {
                    var fresh = await _tokenProvider.GetAccessTokenAsync("Jira", _userId.Value);
                    if (!string.IsNullOrEmpty(fresh))
                        _apiToken = fresh;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Pre-fetch Jira token failed: {Msg}", ex.Message);
                }
            }

            SetAuth(_apiToken);

            _logger.LogInformation(
                "Creating Jira ticket | URL={Url} | OAuth={OAuth} | TokenLen={Len} | Project={Key}",
                url, isOAuth, _apiToken.Length, _projectKey);

            var json = JsonSerializer.Serialize(payload);
            var res  = await http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

            // On 401 for OAuth: force-refresh via TokenProvider and retry once
            if (res.StatusCode == HttpStatusCode.Unauthorized && isOAuth)
            {
                _logger.LogWarning("Jira 401 — attempting OAuth token refresh");
                string? newToken = null;
                if (_userId.HasValue)
                {
                    try { newToken = await _tokenProvider.GetAccessTokenAsync("Jira", _userId.Value, forceRefresh: true); }
                    catch (Exception ex) { _logger.LogError("Jira force-refresh failed: {Msg}", ex.Message); }
                }

                if (string.IsNullOrEmpty(newToken))
                    throw new InvalidOperationException(
                        "Jira OAuth token expired and refresh failed — re-authorize via Integrations.");

                SetAuth(newToken);
                res = await http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

                if (res.StatusCode == HttpStatusCode.Unauthorized)
                    throw new InvalidOperationException(
                        "Jira API still 401 after token refresh — check OAuth scopes or re-authorize.");
            }

            var raw = await res.Content.ReadAsStringAsync();

            _logger.LogInformation(
                "Jira response {Code}: {Preview}",
                (int)res.StatusCode,
                raw[..Math.Min(300, raw.Length)]);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Jira FAILED {Code} | Domain={D} | Project={P} | " +
                    "Email={E} | Body={Raw}",
                    (int)res.StatusCode,
                    _domain, _projectKey, _email, raw);

                return new JiraTicket
                {
                    Key = "ERROR",
                    Summary = bugSummary,
                    Status = $"Failed: {(int)res.StatusCode} — {raw}"
                };
            }

            using var doc = JsonDocument.Parse(raw);
            var key = doc.RootElement
                               .GetProperty("key")
                               .GetString() ?? "UNKNOWN";
            var ticketUrl = isOAuth && !string.IsNullOrEmpty(_siteUrl)
                ? $"{_siteUrl}/browse/{key}"
                : $"https://{_domain}/browse/{key}";

            _logger.LogInformation(
                " Jira ticket created: {Key} → {Url}", key, ticketUrl);

            return new JiraTicket
            {
                Key = key,
                Summary = summary,
                Description = $"Client: {clientName}\nBug: {bugSummary}",
                Priority = priority,
                Status = "Open",
                Reporter = _email,
                Url = ticketUrl,
                Created = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                "CreateTicket exception: {Msg}", ex.Message);
            return new JiraTicket
            {
                Key = "ERROR",
                Summary = bugSummary,
                Status = $"Exception: {ex.Message}"
            };
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string TruncateSummary(string summary)
    {
        var clean = summary.Replace("\n", " ").Trim();
        return clean.Length > 80 ? clean[..80] + "..." : clean;
    }

    private static string DetectPriority(string summary)
    {
        var lower = summary.ToLower();

        if (lower.Contains("crash") || lower.Contains("down") ||
            lower.Contains("not working") || lower.Contains("broken") ||
            lower.Contains("payment") || lower.Contains("critical"))
            return "High";

        if (lower.Contains("slow") || lower.Contains("ui") ||
            lower.Contains("display"))
            return "Medium";

        return "Medium";
    }
}

public class JiraConfig
{
    public string BaseUrl { get; set; } = "";
    public string Email { get; set; } = "";
    public string ApiToken { get; set; } = "";
    public string ProjectKey { get; set; } = "";
}
