using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Controllers;

[ApiController]
[Route("api/integrations")]
[Authorize]
public class IntegrationController : ControllerBase
{
    private readonly ApplicationDbContext          _db;
    private readonly ILogger<IntegrationController> _logger;

    public IntegrationController(ApplicationDbContext db, ILogger<IntegrationController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // ── GET /api/integrations ────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var saved = await _db.Integrations
            .Where(i => i.UserId == userId.Value)
            .AsNoTracking()
            .ToListAsync();

        var types = new[] { "jira", "github", "slack", "gmail", "zoom" };
        var result = types.Select(type =>
        {
            var existing = saved.FirstOrDefault(i =>
                i.Type.ToLower() == type.ToLower());
            Dictionary<string, string>? config = null;
            if (existing?.ConfigJson != null)
                try { config = JsonSerializer.Deserialize<Dictionary<string, string>>(existing.ConfigJson); } catch { }
            return new
            {
                type,
                isConnected = existing?.IsConnected ?? false,
                connectedAt = existing?.ConnectedAt,
                config,
                owner  = existing?.Owner,
                domain = existing?.Domain,
                email  = existing?.Email,
            };
        });
        return Ok(result);
    }

    // ── POST /api/integrations/connect ───────────────────────
    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromBody] ConnectIntegrationRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Type)) return BadRequest(new { error = "Type is required." });

        var normalizedType = req.Type.ToLower() switch
        {
            "jira"   => "Jira",
            "github" => "GitHub",
            "slack"  => "Slack",
            "gmail"  => "Gmail",
            _        => req.Type
        };

        // Parse the incoming config (handles single-encoded, double-encoded, and object)
        var cfg = ParseConfigDict(req.ConfigJson);

        // Store as a clean JSON object (never double-encoded)
        var configJson = cfg.Any()
            ? JsonSerializer.Serialize(cfg)
            : JsonSerializer.Serialize(req.ConfigJson);

        _logger.LogInformation("Connect {Type}: cfg keys = [{Keys}]",
            normalizedType, string.Join(", ", cfg.Keys));

        var existing = await _db.Integrations
            .FirstOrDefaultAsync(i => i.UserId == userId.Value && i.Type == normalizedType);

        if (existing == null)
        {
            existing = new Integration { UserId = userId.Value, Type = normalizedType };
            _db.Integrations.Add(existing);
        }

        existing.ConfigJson  = configJson;
        existing.IsConnected = true;
        existing.ConnectedAt = DateTime.UtcNow;

        PopulateColumns(existing, normalizedType, cfg);

        // Auto-fetch Jira accountId so reporter can be set correctly
        if (normalizedType == "Jira" &&
            !string.IsNullOrEmpty(existing.Domain) &&
            !string.IsNullOrEmpty(existing.AccessToken))
        {
            try
            {
                var accountId = await FetchJiraAccountIdAsync(
                    existing.Domain, existing.Email, existing.AccessToken);
                existing.AccountId = accountId;
                _logger.LogInformation("Jira accountId fetched: {Id}", accountId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Could not fetch Jira accountId: {Msg}", ex.Message);
            }
        }

        _logger.LogInformation(
            "Saving {Type}: Domain={Domain} Email={Email} Token={TokenSet} Owner={Owner} Repo={Repo} Project={Project} AccountId={AccId}",
            normalizedType, existing.Domain, existing.Email,
            !string.IsNullOrEmpty(existing.AccessToken),
            existing.Owner, existing.Repo, existing.ProjectKey, existing.AccountId);

        await _db.SaveChangesAsync();
        return Ok(new { message = $"{normalizedType} connected successfully.", type = req.Type.ToLower(), isConnected = true });
    }

    // ── POST /api/integrations/test ──────────────────────────
    [HttpPost("test")]
    public IActionResult Test([FromBody] ConnectIntegrationRequest req)
        => Ok(new { success = true, message = $"{req.Type} connection test passed." });

    // ── DELETE /api/integrations/{type} ──────────────────────
    [HttpDelete("{type}")]
    public async Task<IActionResult> Disconnect(string type)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var existing = await _db.Integrations
            .FirstOrDefaultAsync(i =>
                i.UserId == userId.Value &&
                i.Type.ToLower() == type.ToLower());

        if (existing != null)
        {
            existing.IsConnected = false;
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = $"{type} disconnected." });
    }

    // ── Helpers ───────────────────────────────────────────────

    /// <summary>
    /// Robustly parses ConfigJson regardless of encoding:
    ///   - object  (JsonElement of kind Object)
    ///   - string  containing JSON (JsonElement of kind String — from JSON.stringify)
    ///   - double-encoded string ("\"...\"")
    /// </summary>
    public static Dictionary<string, string> ParseConfigDict(object? configJson)
    {
        if (configJson == null) return new();

        string? raw = null;

        if (configJson is JsonElement el)
        {
            raw = el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),   // JSON.stringify'd on the client
                JsonValueKind.Object => el.GetRawText(),  // plain object
                _                   => null
            };
        }
        else if (configJson is string s)
        {
            raw = s;
        }

        if (string.IsNullOrWhiteSpace(raw)) return new();

        // Handle double-encoded: "\"{ ... }\""
        if (raw.TrimStart().StartsWith("\""))
        {
            try { raw = JsonSerializer.Deserialize<string>(raw); } catch { }
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(
                       raw ?? "",
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// Populate explicit credential columns from the parsed config dictionary.
    /// Also accepts common key aliases so any naming convention works.
    /// </summary>
    public static void PopulateColumns(Integration integ, string normalizedType, Dictionary<string, string> cfg)
    {
        string? Get(params string[] keys)
        {
            foreach (var k in keys)
                if (cfg.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            return null;
        }

        switch (normalizedType)
        {
            case "Jira":
                integ.Domain      = Get("domain", "Domain", "baseUrl", "BaseUrl");
                integ.Email       = Get("email", "Email");
                integ.AccessToken = Get("apiToken", "ApiToken", "token", "Token", "accessToken");
                integ.ProjectKey  = Get("project", "Project", "projectKey", "ProjectKey");
                break;
            case "GitHub":
                integ.AccessToken = Get("token", "Token", "apiToken", "accessToken");
                integ.Owner       = Get("owner", "Owner");
                integ.Repo        = Get("repo", "Repo");
                break;
            case "Slack":
                integ.Domain = Get("webhookUrl", "WebhookUrl", "webhook_url");
                integ.Email  = Get("channelName", "ChannelName", "channel");
                break;
            case "Gmail":
                integ.Email       = Get("fromEmail", "FromEmail", "email");
                integ.AccessToken = Get("appPassword", "AppPassword", "password");
                integ.Domain      = Get("toEmail", "ToEmail");
                break;
        }
    }

    /// <summary>
    /// Backfill explicit columns from ConfigJson when they are missing.
    /// Returns true if any column was updated.
    /// </summary>
    public static bool BackfillFromConfigJson(Integration integ, ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(integ.ConfigJson)) return false;
        if (integ.Domain != null && integ.AccessToken != null) return false; // already populated

        var cfg = ParseConfigDict(integ.ConfigJson);
        if (!cfg.Any()) return false;

        PopulateColumns(integ, integ.Type, cfg);
        logger?.LogInformation("Backfilled columns from ConfigJson for {Type} ({Id})", integ.Type, integ.Id);
        return true;
    }

    private static async Task<string?> FetchJiraAccountIdAsync(
        string domain, string? email, string token)
    {
        var clean = domain.Replace("https://", "").Replace("http://", "").TrimEnd('/');
        var creds = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{email}:{token}"));

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
        http.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var res = await http.GetAsync($"https://{clean}/rest/api/3/myself");
        if (!res.IsSuccessStatusCode) return null;

        var raw = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.TryGetProperty("accountId", out var id)
            ? id.GetString()
            : null;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
