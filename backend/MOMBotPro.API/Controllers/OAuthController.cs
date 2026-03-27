using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models;
using MOMBotPro.API.Services;

namespace MOMBotPro.API.Controllers;

[ApiController]
[Route("api/oauth")]
public class OAuthController : ControllerBase
{
    private readonly ApplicationDbContext     _db;
    private readonly IConfiguration           _config;
    private readonly IHttpClientFactory       _httpFactory;
    private readonly ILogger<OAuthController> _logger;
    private readonly ZoomSyncService          _zoomSync;

    private const string FrontendBase = "http://localhost:3000/integrations";

    public OAuthController(
        ApplicationDbContext     db,
        IConfiguration           config,
        IHttpClientFactory       httpFactory,
        ILogger<OAuthController> logger,
        ZoomSyncService          zoomSync)
    {
        _db          = db;
        _config      = config;
        _httpFactory = httpFactory;
        _logger      = logger;
        _zoomSync    = zoomSync;
    }

    // ══════════════════════════════════════════════════════════
    // GITHUB  (https://docs.github.com/en/apps/oauth-apps/
    //          building-oauth-apps/authorizing-oauth-apps)
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Step 1 — browser navigates here with ?token=&lt;jwt&gt;.
    /// Generates PKCE + state, persists to DB, redirects (302) to GitHub.
    /// </summary>
    [Authorize]
    [HttpGet("github/auth")]
    public async Task<IActionResult> GitHubAuth()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // GitHub OAuth Apps do NOT support PKCE — state-only CSRF protection
        var state    = await PersistStateAsync("github", userId.Value, codeVerifier: null);
        var clientId = _config["GitHub:ClientId"];
        var callback = _config["GitHub:CallbackUrl"] ?? CallbackUrl("github");

        var url = "https://github.com/login/oauth/authorize" +
                  $"?client_id={Uri.EscapeDataString(clientId ?? "")}" +
                  $"&redirect_uri={Uri.EscapeDataString(callback)}" +
                  $"&scope={Uri.EscapeDataString("repo user:email")}" +
                  $"&state={state}";

        _logger.LogInformation("GitHub OAuth URL: {Url}", url);
        return Redirect(url);
    }

    /// <summary>
    /// Step 2 — GitHub redirects here with ?code=&amp;state=.
    /// Validates state, exchanges code (+ PKCE verifier) for access token,
    /// fetches GitHub login, persists to Integrations, redirects to frontend.
    /// </summary>
    [HttpGet("github/callback")]
    public async Task<IActionResult> GitHubCallback(
        [FromQuery] string? code, [FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest("Missing code or state parameter.");

        var (userId, verifier, stateError) = await ConsumeStateAsync(state, "github");
        if (stateError != null)
        {
            _logger.LogWarning("GitHub state validation failed: {Err}", stateError);
            return BadRequest($"OAuth state error: {stateError}");
        }

        try
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            http.DefaultRequestHeaders.Add("User-Agent", "MOMBotPro/1.0");

            // Exchange code → access_token (includes PKCE verifier)
            var tokenRes = await http.PostAsJsonAsync(
                "https://github.com/login/oauth/access_token",
                new
                {
                    client_id     = _config["GitHub:ClientId"],
                    client_secret = _config["GitHub:ClientSecret"],
                    code,
                    redirect_uri  = _config["GitHub:CallbackUrl"] ?? CallbackUrl("github")
                });

            var tokenRaw = await tokenRes.Content.ReadAsStringAsync();
            _logger.LogDebug("GitHub token response: {Raw}", tokenRaw);

            using var tokenDoc  = JsonDocument.Parse(tokenRaw);
            var accessToken     = tokenDoc.RootElement.GetProperty("access_token").GetString();

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("GitHub token exchange returned no access_token. Raw: {Raw}", tokenRaw);
                return Redirect($"{FrontendBase}?error=github_token_failed");
            }

            // Fetch the authenticated GitHub username
            var ghClient = _httpFactory.CreateClient();
            ghClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            ghClient.DefaultRequestHeaders.Add("User-Agent", "MOMBotPro/1.0");

            var ghUserRes = await ghClient.GetAsync("https://api.github.com/user");
            var ghUserRaw = await ghUserRes.Content.ReadAsStringAsync();
            using var ghUserDoc = JsonDocument.Parse(ghUserRaw);
            var ghLogin = ghUserDoc.RootElement.TryGetProperty("login", out var lg)
                          ? lg.GetString() : null;

            await SaveIntegrationAsync(userId!.Value, "GitHub", integ =>
            {
                integ.AccessToken = accessToken;
                if (ghLogin != null) integ.Owner = ghLogin;
            });

            _logger.LogInformation("GitHub OAuth complete — user={UserId} login={Login}",
                userId, ghLogin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub callback failed");
            return Redirect($"{FrontendBase}?error=github_callback_failed");
        }

        return Redirect($"{FrontendBase}?github=connected");
    }

    // ══════════════════════════════════════════════════════════
    // JIRA (Atlassian OAuth 2.0 3LO + PKCE)
    // ══════════════════════════════════════════════════════════

    [Authorize]
    [HttpGet("jira/auth")]
    public async Task<IActionResult> JiraAuth()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (verifier, challenge) = GeneratePkce();
        var state    = await PersistStateAsync("jira", userId.Value, verifier);
        var clientId = _config["Jira:ClientId"];
        var callback = _config["Jira:CallbackUrl"] ?? CallbackUrl("jira");
        var scope    = "read:jira-work write:jira-work read:me offline_access";

        var url = "https://auth.atlassian.com/authorize" +
                  $"?audience={Uri.EscapeDataString("api.atlassian.com")}" +
                  $"&client_id={Uri.EscapeDataString(clientId ?? "")}" +
                  $"&scope={Uri.EscapeDataString(scope)}" +
                  $"&redirect_uri={Uri.EscapeDataString(callback)}" +
                  $"&state={state}" +
                  $"&response_type=code" +
                  $"&prompt=consent" +
                  $"&code_challenge={challenge}" +
                  $"&code_challenge_method=S256";

        _logger.LogInformation("Jira OAuth initiated for user {UserId}", userId);
        return Redirect(url);
    }

    [HttpGet("jira/callback")]
    public async Task<IActionResult> JiraCallback(
        [FromQuery] string? code, [FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest("Missing code or state parameter.");

        var (userId, verifier, stateError) = await ConsumeStateAsync(state, "jira");
        if (stateError != null)
        {
            _logger.LogWarning("Jira state validation failed: {Err}", stateError);
            return BadRequest($"OAuth state error: {stateError}");
        }

        try
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            var tokenRes = await http.PostAsJsonAsync(
                "https://auth.atlassian.com/oauth/token",
                new
                {
                    grant_type    = "authorization_code",
                    client_id     = _config["Jira:ClientId"],
                    client_secret = _config["Jira:ClientSecret"],
                    code,
                    redirect_uri  = _config["Jira:CallbackUrl"] ?? CallbackUrl("jira"),
                    code_verifier = verifier
                });

            var tokenRaw = await tokenRes.Content.ReadAsStringAsync();
            _logger.LogDebug("Jira token response: {Raw}", tokenRaw);
            using var tokenDoc   = JsonDocument.Parse(tokenRaw);
            var accessToken      = tokenDoc.RootElement.GetProperty("access_token").GetString();
            var refreshToken     = tokenDoc.RootElement.TryGetProperty("refresh_token", out var rtEl)
                                   ? rtEl.GetString() : null;

            // Resolve Atlassian cloud_id and site URL
            var resourceClient = _httpFactory.CreateClient();
            resourceClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);
            resourceClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            var resRes = await resourceClient.GetAsync(
                "https://api.atlassian.com/oauth/token/accessible-resources");
            var resRaw = await resRes.Content.ReadAsStringAsync();
            _logger.LogDebug("Jira accessible-resources response: {Raw}", resRaw);
            using var resDoc = JsonDocument.Parse(resRaw);

            var firstSite = resDoc.RootElement.EnumerateArray().FirstOrDefault();
            var cloudId   = firstSite.ValueKind != JsonValueKind.Undefined &&
                            firstSite.TryGetProperty("id",  out var idEl)  ? idEl.GetString()  : null;
            var siteUrl   = firstSite.ValueKind != JsonValueKind.Undefined &&
                            firstSite.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : null;

            await SaveIntegrationAsync(userId!.Value, "Jira", integ =>
            {
                integ.AccessToken = accessToken;
                integ.Domain      = cloudId;   // UUID — used as cloud_id in API calls
                integ.AccountId   = siteUrl;   // e.g. https://mysite.atlassian.net
                // Persist refresh_token in ConfigJson so RealJiraService can renew expired access tokens
                integ.ConfigJson  = JsonSerializer.Serialize(new { refresh_token = refreshToken ?? "" });
                // Email left null intentionally — signals OAuth mode to RealJiraService
            });

            _logger.LogInformation("Jira OAuth complete — user={UserId} cloudId={CloudId} siteUrl={Url}",
                userId, cloudId, siteUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jira callback failed");
            return Redirect($"{FrontendBase}?error=jira_callback_failed");
        }

        return Redirect($"{FrontendBase}?jira=connected");
    }

    // ══════════════════════════════════════════════════════════
    // GMAIL (Google OAuth 2.0 + PKCE)
    // ══════════════════════════════════════════════════════════

    [Authorize]
    [HttpGet("gmail/auth")]
    public async Task<IActionResult> GmailAuth()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (verifier, challenge) = GeneratePkce();
        var state    = await PersistStateAsync("gmail", userId.Value, verifier);
        var clientId = _config["Google:ClientId"];
        var callback = _config["Google:CallbackUrl"] ?? CallbackUrl("gmail");

        var url = "https://accounts.google.com/o/oauth2/v2/auth" +
                  $"?client_id={Uri.EscapeDataString(clientId ?? "")}" +
                  $"&redirect_uri={Uri.EscapeDataString(callback)}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString("https://www.googleapis.com/auth/gmail.send")}" +
                  $"&access_type=offline" +
                  $"&prompt=consent" +
                  $"&state={state}" +
                  $"&code_challenge={challenge}" +
                  $"&code_challenge_method=S256";

        _logger.LogInformation("Gmail OAuth initiated for user {UserId}", userId);
        return Redirect(url);
    }

    [HttpGet("gmail/callback")]
    public async Task<IActionResult> GmailCallback(
        [FromQuery] string? code, [FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest("Missing code or state parameter.");

        var (userId, verifier, stateError) = await ConsumeStateAsync(state, "gmail");
        if (stateError != null)
        {
            _logger.LogWarning("Gmail state validation failed: {Err}", stateError);
            return BadRequest($"OAuth state error: {stateError}");
        }

        try
        {
            var http = _httpFactory.CreateClient();

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"]          = code,
                ["client_id"]     = _config["Google:ClientId"]     ?? "",
                ["client_secret"] = _config["Google:ClientSecret"] ?? "",
                ["redirect_uri"]  = _config["Google:CallbackUrl"]  ?? CallbackUrl("gmail"),
                ["grant_type"]    = "authorization_code",
                ["code_verifier"] = verifier ?? ""
            });

            var tokenRes = await http.PostAsync("https://oauth2.googleapis.com/token", form);
            var tokenRaw = await tokenRes.Content.ReadAsStringAsync();
            using var tokenDoc = JsonDocument.Parse(tokenRaw);

            // Store the refresh_token — access tokens expire; refresh tokens are durable
            var refreshToken = tokenDoc.RootElement.TryGetProperty("refresh_token", out var rt)
                               ? rt.GetString() : null;

            // Fetch the user's Gmail address with the short-lived access_token
            string? email = null;
            if (tokenDoc.RootElement.TryGetProperty("access_token", out var at))
            {
                var infoClient = _httpFactory.CreateClient();
                infoClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", at.GetString());
                var infoRes = await infoClient.GetAsync(
                    "https://www.googleapis.com/userinfo/v2/me");
                var infoRaw = await infoRes.Content.ReadAsStringAsync();
                using var infoDoc = JsonDocument.Parse(infoRaw);
                email = infoDoc.RootElement.TryGetProperty("email", out var em)
                        ? em.GetString() : null;
            }

            await SaveIntegrationAsync(userId!.Value, "Gmail", integ =>
            {
                integ.AccessToken = refreshToken;
                if (email != null) integ.Email = email;
            });

            _logger.LogInformation("Gmail OAuth complete — user={UserId} email={Email}", userId, email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail callback failed");
            return Redirect($"{FrontendBase}?error=gmail_callback_failed");
        }

        return Redirect($"{FrontendBase}?gmail=connected");
    }

    // ══════════════════════════════════════════════════════════
    // SLACK (state only — Slack v2 does not support PKCE)
    // ══════════════════════════════════════════════════════════

    [Authorize]
    [HttpGet("slack/auth")]
    public async Task<IActionResult> SlackAuth()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Slack does not support PKCE; use state-only CSRF protection
        var state    = await PersistStateAsync("slack", userId.Value, codeVerifier: null);
        var clientId = _config["Slack:ClientId"];
        var callback = _config["Slack:CallbackUrl"] ?? CallbackUrl("slack");

        var url = "https://slack.com/oauth/v2/authorize" +
                  $"?client_id={Uri.EscapeDataString(clientId ?? "")}" +
                  $"&scope={Uri.EscapeDataString("chat:write channels:read incoming-webhook")}" +
                  $"&redirect_uri={Uri.EscapeDataString(callback)}" +
                  $"&state={state}";

        _logger.LogInformation("Slack OAuth initiated for user {UserId}", userId);
        return Redirect(url);
    }

    [HttpGet("slack/callback")]
    public async Task<IActionResult> SlackCallback(
        [FromQuery] string? code, [FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest("Missing code or state parameter.");

        var (userId, _, stateError) = await ConsumeStateAsync(state, "slack");
        if (stateError != null)
        {
            _logger.LogWarning("Slack state validation failed: {Err}", stateError);
            return BadRequest($"OAuth state error: {stateError}");
        }

        try
        {
            var http = _httpFactory.CreateClient();

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"]          = code,
                ["client_id"]     = _config["Slack:ClientId"]     ?? "",
                ["client_secret"] = _config["Slack:ClientSecret"] ?? "",
                ["redirect_uri"]  = _config["Slack:CallbackUrl"]  ?? CallbackUrl("slack")
            });

            var tokenRes = await http.PostAsync("https://slack.com/api/oauth.v2.access", form);
            var tokenRaw = await tokenRes.Content.ReadAsStringAsync();
            using var tokenDoc = JsonDocument.Parse(tokenRaw);

            var botToken   = tokenDoc.RootElement.TryGetProperty("access_token", out var bt)
                             ? bt.GetString() : null;
            string? webhookUrl = null;
            string? channel    = null;
            if (tokenDoc.RootElement.TryGetProperty("incoming_webhook", out var wh))
            {
                webhookUrl = wh.TryGetProperty("url",     out var wu) ? wu.GetString() : null;
                channel    = wh.TryGetProperty("channel", out var ch) ? ch.GetString() : null;
            }

            await SaveIntegrationAsync(userId!.Value, "Slack", integ =>
            {
                integ.AccessToken = botToken;
                integ.Domain      = webhookUrl;
                if (channel != null) integ.Email = channel;
            });

            _logger.LogInformation("Slack OAuth complete — user={UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Slack callback failed");
            return Redirect($"{FrontendBase}?error=slack_callback_failed");
        }

        return Redirect($"{FrontendBase}?slack=connected");
    }

    // ══════════════════════════════════════════════════════════
    // ZOOM  (state-only CSRF — Zoom does not support PKCE)
    // ══════════════════════════════════════════════════════════

    [Authorize]
    [HttpGet("zoom/auth")]
    public async Task<IActionResult> ZoomAuth()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var state    = await PersistStateAsync("zoom", userId.Value, codeVerifier: null);
        var clientId = _config["Zoom:ClientId"];
        var callback = _config["Zoom:CallbackUrl"] ?? CallbackUrl("zoom");

        var url = "https://zoom.us/oauth/authorize" +
                  $"?response_type=code" +
                  $"&client_id={Uri.EscapeDataString(clientId ?? "")}" +
                  $"&redirect_uri={Uri.EscapeDataString(callback)}" +
                  $"&scope={Uri.EscapeDataString("meeting:read:list_meetings meeting:read:meeting recording:read:list_recordings recording:read:recording user:read:user")}" +
                  $"&state={state}";

        _logger.LogInformation("Zoom OAuth initiated for user {UserId}", userId);
        return Redirect(url);
    }

    [HttpGet("zoom/callback")]
    public async Task<IActionResult> ZoomCallback(
        [FromQuery] string? code, [FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest("Missing code or state.");

        var (userId, _, stateError) = await ConsumeStateAsync(state, "zoom");
        if (stateError != null)
        {
            _logger.LogWarning("Zoom state validation failed: {Err}", stateError);
            return BadRequest($"OAuth state error: {stateError}");
        }

        try
        {
            var clientId     = _config["Zoom:ClientId"]     ?? "";
            var clientSecret = _config["Zoom:ClientSecret"] ?? "";
            var callback     = _config["Zoom:CallbackUrl"]  ?? CallbackUrl("zoom");

            // Zoom uses HTTP Basic auth for token exchange (not JSON body)
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]   = "authorization_code",
                ["code"]         = code,
                ["redirect_uri"] = callback,
            });

            var tokenRes = await http.PostAsync("https://zoom.us/oauth/token", form);
            var tokenRaw = await tokenRes.Content.ReadAsStringAsync();
            _logger.LogDebug("Zoom token response: {Raw}", tokenRaw);

            using var tokenDoc = JsonDocument.Parse(tokenRaw);
            var accessToken    = tokenDoc.RootElement.GetProperty("access_token").GetString();
            var refreshToken   = tokenDoc.RootElement.TryGetProperty("refresh_token", out var rt)
                                 ? rt.GetString() : null;

            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Zoom token exchange failed. Raw: {Raw}", tokenRaw);
                return Redirect($"{FrontendBase}?error=zoom_token_failed");
            }

            // Save token first — so a failed /me call can't block the access token from being stored
            await SaveIntegrationAsync(userId!.Value, "Zoom", integ =>
            {
                integ.AccessToken = accessToken;
                integ.ConfigJson  = JsonSerializer.Serialize(new { refresh_token = refreshToken ?? "" });
            });

            // Fetch Zoom user profile for email + Zoom user ID (best-effort — non-fatal)
            string? email  = null;
            string? zoomId = null;
            try
            {
                var meClient = _httpFactory.CreateClient();
                meClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                meClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var meRes = await meClient.GetAsync("https://api.zoom.us/v2/users/me");
                var meRaw = await meRes.Content.ReadAsStringAsync();
                _logger.LogInformation("Zoom /me response {Status}: {Raw}",
                    (int)meRes.StatusCode, meRaw[..Math.Min(300, meRaw.Length)]);

                if (meRes.IsSuccessStatusCode)
                {
                    using var meDoc = JsonDocument.Parse(meRaw);
                    email  = meDoc.RootElement.TryGetProperty("email", out var em) ? em.GetString() : null;
                    zoomId = meDoc.RootElement.TryGetProperty("id",    out var id) ? id.GetString() : null;

                    if (email != null || zoomId != null)
                    {
                        await SaveIntegrationAsync(userId!.Value, "Zoom", integ =>
                        {
                            if (email  != null) integ.Email = email;
                            if (zoomId != null) integ.Owner = zoomId; // Zoom user ID — used for webhook routing
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Zoom /me profile fetch failed — token saved, email/owner not set");
            }

            // Sync calendar immediately so meetings are ready on the integrations page
            try { await _zoomSync.FetchAndSyncMeetingsAsync(userId!.Value); }
            catch (Exception ex) { _logger.LogWarning(ex, "Initial Zoom sync failed — background will retry"); }

            _logger.LogInformation("Zoom OAuth complete — user={UserId} email={Email}", userId, email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Zoom callback failed");
            return Redirect($"{FrontendBase}?error=zoom_callback_failed");
        }

        return Redirect($"{FrontendBase}?zoom=connected");
    }

    // ══════════════════════════════════════════════════════════
    // PKCE helpers
    // ══════════════════════════════════════════════════════════

    /// <summary>
    /// Generates a cryptographically secure PKCE code_verifier and its S256 challenge.
    /// Verifier: 32 random bytes → base64url (43 chars, within RFC 7636's 43-128 range).
    /// Challenge: SHA-256(verifier) → base64url.
    /// </summary>
    private static (string verifier, string challenge) GeneratePkce()
    {
        Span<byte> verifierBytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(verifierBytes);
        var verifier  = Base64UrlEncode(verifierBytes.ToArray());

        var hash      = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64UrlEncode(hash);

        return (verifier, challenge);
    }

    private static string GenerateSecureState()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes.ToArray());
    }

    // Base64url encoding: no padding, + → -, / → _
    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
               .Replace('+', '-')
               .Replace('/', '_')
               .TrimEnd('=');

    // ══════════════════════════════════════════════════════════
    // State management
    // ══════════════════════════════════════════════════════════

    private async Task<string> PersistStateAsync(
        string provider, Guid userId, string? codeVerifier)
    {
        // Remove any stale states for this user+provider before creating a new one
        var stale = await _db.OAuthStates
            .Where(o => o.UserId == userId && o.Provider == provider)
            .ToListAsync();
        _db.OAuthStates.RemoveRange(stale);

        var entry = new OAuthState
        {
            State        = GenerateSecureState(),
            Provider     = provider,
            UserId       = userId,
            CodeVerifier = codeVerifier,
            CreatedAt    = DateTime.UtcNow,
            ExpiresAt    = DateTime.UtcNow.AddMinutes(10) // GitHub code expires in 10 min
        };
        _db.OAuthStates.Add(entry);
        await _db.SaveChangesAsync();
        return entry.State;
    }

    /// <summary>
    /// Looks up the state row, validates expiry, deletes it (single-use), and returns
    /// the associated userId + codeVerifier. Returns an error string on failure.
    /// </summary>
    private async Task<(Guid? UserId, string? CodeVerifier, string? Error)> ConsumeStateAsync(
        string state, string provider)
    {
        var entry = await _db.OAuthStates
            .FirstOrDefaultAsync(o => o.State == state && o.Provider == provider);

        if (entry == null)
            return (null, null, "invalid_state");

        // Delete immediately — single-use regardless of outcome
        _db.OAuthStates.Remove(entry);
        await _db.SaveChangesAsync();

        if (entry.ExpiresAt < DateTime.UtcNow)
            return (null, null, "state_expired");

        return (entry.UserId, entry.CodeVerifier, null);
    }

    // ══════════════════════════════════════════════════════════
    // Integration persistence
    // ══════════════════════════════════════════════════════════

    private async Task SaveIntegrationAsync(
        Guid userId, string type, Action<Integration> apply)
    {
        var existing = await _db.Integrations
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Type == type);

        if (existing == null)
        {
            existing = new Integration { UserId = userId, Type = type };
            _db.Integrations.Add(existing);
        }

        apply(existing);
        existing.IsConnected = true;
        existing.ConnectedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    // ══════════════════════════════════════════════════════════
    // Config helpers
    // ══════════════════════════════════════════════════════════

    private string CallbackUrl(string provider)
    {
        var backendUrl = _config["App:BackendUrl"] ?? "http://localhost:5000";
        return $"{backendUrl}/api/oauth/{provider}/callback";
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
