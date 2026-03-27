using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Services;

public class GitHubService
{
    private readonly HttpClient              _http;
    private readonly IConfiguration         _config;
    private readonly ITokenProvider          _tokenProvider;
    private readonly ILogger<GitHubService> _logger;
    private string _owner;
    private string _repo;
    private string _token;
    private Guid?  _userId;
    private const string BASE = "https://api.github.com";

    public GitHubService(
        IHttpClientFactory       factory,
        IConfiguration           config,
        ITokenProvider           tokenProvider,
        ILogger<GitHubService>   logger)
    {
        _config        = config;
        _tokenProvider = tokenProvider;
        _logger        = logger;
        _owner  = config["GitHub:Owner"] ?? "Ramu-9346";
        _repo   = config["GitHub:Repo"]  ?? "GlowCart";
        _token  = config["GitHub:Token"] ?? "";
        _http   = factory.CreateClient("github");
        SetupClient();
    }

    private void SetupClient()
    {
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        _http.DefaultRequestHeaders.Add("User-Agent",    "MOMBotPro/1.0");
        _http.DefaultRequestHeaders.Add("Accept",        "application/vnd.github.v3+json");
        // Bug 2: guard BaseAddress — HttpClient throws InvalidOperationException if set
        // after a request has already been made on this instance.
        if (_http.BaseAddress == null)
            _http.BaseAddress = new Uri(BASE);
    }

    public void Configure(string owner, string repo, string token)
    {
        _owner  = owner;
        _repo   = repo;
        _token  = token;
        _userId = null;
        SetupClient();
    }

    /// <summary>Configure using userId — token is fetched fresh via ITokenProvider.</summary>
    public void Configure(string owner, string repo, Guid userId)
    {
        _owner  = owner;
        _repo   = repo;
        _userId = userId;
        // _token stays as fallback; will be replaced by EnsureFreshTokenAsync before each call
    }

    private async Task EnsureFreshTokenAsync()
    {
        if (!_userId.HasValue) return;
        try
        {
            var fresh = await _tokenProvider.GetAccessTokenAsync("GitHub", _userId.Value);
            if (!string.IsNullOrEmpty(fresh) && fresh != _token)
            {
                _token = fresh;
                SetupClient();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GitHub token refresh failed: {Msg}", ex.Message);
        }
    }

    // ── Bug 3: resolve repo's actual default branch instead of hardcoding "master" ──
    private async Task<string> GetDefaultBranch()
    {
        try
        {
            var res = await _http.GetAsync($"/repos/{_owner}/{_repo}");
            var raw = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("default_branch", out var db)
                ? db.GetString() ?? "main"
                : "main";
        }
        catch (Exception ex)
        {
            _logger.LogWarning("GetDefaultBranch failed, falling back to 'main': {Msg}", ex.Message);
            return "main";
        }
    }

    // ── STEP 1: Get codebase context for bug analysis ─────────────────────
    public async Task<string> GetCodebaseContext(string bugSummary)
    {
        await EnsureFreshTokenAsync();
        try
        {
            _logger.LogInformation("Scanning {Owner}/{Repo} for: {Bug}", _owner, _repo, bugSummary);

            var defaultBranch = await GetDefaultBranch();

            // Get repo file tree from default branch
            var treeRes = await _http.GetAsync($"/repos/{_owner}/{_repo}/git/trees/{defaultBranch}?recursive=1");
            var treeRaw = await treeRes.Content.ReadAsStringAsync();

            using var treeDoc = JsonDocument.Parse(treeRaw);
            var files = treeDoc.RootElement
                .GetProperty("tree")
                .EnumerateArray()
                .Where(f =>
                    f.GetProperty("type").GetString() == "blob" &&
                    IsRelevantFile(f.GetProperty("path").GetString() ?? ""))
                .Select(f => f.GetProperty("path").GetString())
                .Take(50)
                .ToList();

            _logger.LogInformation("Found {N} relevant files", files.Count);

            var sb = new StringBuilder();
            sb.AppendLine($"Repository: {_owner}/{_repo}");
            sb.AppendLine($"Bug: {bugSummary}");
            sb.AppendLine("---");

            var relevantFiles = files
                .Where(f => IsFileRelevantToBug(f, bugSummary))
                .Take(5)
                .ToList();

            foreach (var filePath in relevantFiles)
            {
                try
                {
                    var content = await GetFileContent(filePath!, defaultBranch);
                    if (!string.IsNullOrEmpty(content))
                    {
                        sb.AppendLine($"\nFile: {filePath}");
                        sb.AppendLine("```csharp");
                        var lines = content.Split('\n').Take(100);
                        sb.AppendLine(string.Join('\n', lines));
                        sb.AppendLine("```");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Could not read {File}: {Msg}", filePath, ex.Message);
                }
            }

            var ctx = sb.ToString();
            _logger.LogInformation("Codebase context: {Len} chars", ctx.Length);
            return ctx;
        }
        catch (Exception ex)
        {
            _logger.LogError("GetCodebaseContext error: {Msg}", ex.Message);
            return $"Repository: {_owner}/{_repo}\nBug: {bugSummary}";
        }
    }

    private static bool IsRelevantFile(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext is ".cs" or ".js" or ".jsx" or ".ts" or ".tsx" or ".html" or ".cshtml";
    }

    private static bool IsFileRelevantToBug(string? path, string bug)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var bugWords = bug.ToLower().Split(' ', '-', '_').Where(w => w.Length > 3);
        var pathLower = path.ToLower();
        return bugWords.Any(w => pathLower.Contains(w));
    }

    private async Task<string> GetFileContent(string filePath, string branch)
    {
        var res = await _http.GetAsync($"/repos/{_owner}/{_repo}/contents/{filePath}?ref={branch}");
        var raw = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        var encoded = doc.RootElement.GetProperty("content").GetString() ?? "";
        var bytes   = Convert.FromBase64String(encoded.Replace("\n", ""));
        return Encoding.UTF8.GetString(bytes);
    }

    // ── STEP 2: Get SHA of the repo's default branch ─────────────────────
    private async Task<string> GetMainSha()
    {
        var defaultBranch = await GetDefaultBranch();
        var res = await _http.GetAsync($"/repos/{_owner}/{_repo}/git/ref/heads/{defaultBranch}");
        var raw = await res.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        var sha = doc.RootElement
            .GetProperty("object")
            .GetProperty("sha")
            .GetString() ?? "";
        _logger.LogInformation("{Branch} SHA: {Sha}", defaultBranch, sha);
        return sha;
    }

    // ── STEP 3: Create branch from default branch ─────────────────────────
    public async Task<string> CreateBranch(string jiraKey, string bugSummary)
    {
        await EnsureFreshTokenAsync();
        var slug = Regex.Replace(bugSummary.ToLower(), @"[^a-z0-9\s-]", "")
            .Trim()
            .Replace(" ", "-");
        if (slug.Length > 40) slug = slug[..40];
        slug = slug.TrimEnd('-');

        var branchName = $"fix/{jiraKey}-{slug}";
        _logger.LogInformation("Creating branch: {Branch}", branchName);

        var masterSha = await GetMainSha();

        var payload = new { @ref = $"refs/heads/{branchName}", sha = masterSha };
        var res = await _http.PostAsJsonAsync($"/repos/{_owner}/{_repo}/git/refs", payload);
        var raw = await res.Content.ReadAsStringAsync();

        if (res.IsSuccessStatusCode)
        {
            _logger.LogInformation("Branch created: {Branch}", branchName);
            return branchName;
        }

        if (raw.Contains("already exists"))
        {
            _logger.LogInformation("Branch already exists: {Branch}", branchName);
            return branchName;
        }

        _logger.LogError("CreateBranch failed {Status}: {Raw}", res.StatusCode, raw);
        throw new Exception($"Failed to create branch: {raw}");
    }

    // ── STEP 4: Commit fix to the new branch ─────────────────────────────
    public async Task CommitFix(
        string branchName,
        string filePath,
        string fixedContent,
        string jiraKey,
        string bugDescription)
    {
        await EnsureFreshTokenAsync();
        try
        {
            _logger.LogInformation("Committing fix to {Branch}/{File}", branchName, filePath);

            // Get current file SHA on the branch (needed to update existing file)
            var fileRes = await _http.GetAsync(
                $"/repos/{_owner}/{_repo}/contents/{filePath}?ref={branchName}");

            string? fileSha = null;
            if (fileRes.IsSuccessStatusCode)
            {
                var fileRaw = await fileRes.Content.ReadAsStringAsync();
                using var fileDoc = JsonDocument.Parse(fileRaw);
                fileSha = fileDoc.RootElement.TryGetProperty("sha", out var s)
                    ? s.GetString() : null;
            }

            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(fixedContent));

            object commitPayload = fileSha != null
                ? new { message = $"fix({jiraKey}): {bugDescription}", content = encoded, sha = fileSha, branch = branchName }
                : new { message = $"fix({jiraKey}): {bugDescription}", content = encoded, branch = branchName };

            var res = await _http.PutAsJsonAsync(
                $"/repos/{_owner}/{_repo}/contents/{filePath}", commitPayload);
            var raw = await res.Content.ReadAsStringAsync();

            if (res.IsSuccessStatusCode)
                _logger.LogInformation("Fix committed to {Branch}", branchName);
            else
                _logger.LogWarning("Commit warning {Status}: {Raw}", res.StatusCode, raw);
        }
        catch (Exception ex)
        {
            _logger.LogError("CommitFix error: {Msg}", ex.Message);
        }
    }

    // ── Bug 4: ensure Uat1 base branch exists; create from default branch if missing ──
    private async Task EnsureUat1BranchExists()
    {
        var checkRes = await _http.GetAsync($"/repos/{_owner}/{_repo}/git/ref/heads/Uat1");
        if (checkRes.StatusCode != System.Net.HttpStatusCode.NotFound) return;

        _logger.LogInformation("Uat1 branch not found — creating from default branch");
        try
        {
            var sha = await GetMainSha();
            var payload = new { @ref = "refs/heads/Uat1", sha };
            var createRes = await _http.PostAsJsonAsync($"/repos/{_owner}/{_repo}/git/refs", payload);
            var raw = await createRes.Content.ReadAsStringAsync();
            if (createRes.IsSuccessStatusCode)
                _logger.LogInformation("Uat1 branch created");
            else if (!raw.Contains("already exists"))
                _logger.LogWarning("Could not create Uat1 branch: {Raw}", raw);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("EnsureUat1BranchExists failed: {Msg}", ex.Message);
        }
    }

    // ── STEP 5: Create PR from fix branch → Uat1 ─────────────────────────
    public async Task<GitHubResult> CreatePR(
        string branchName,
        string jiraKey,
        string title,
        string description)
    {
        await EnsureFreshTokenAsync();
        _logger.LogInformation("Creating PR: {Branch} → Uat1", branchName);

        await EnsureUat1BranchExists();

        var payload = new
        {
            title = $"[{jiraKey}] {title}",
            body  = $"## Jira Ticket\n{jiraKey}\n\n" +
                    $"## Description\n{description}\n\n" +
                    $"## Changes\n- AI-generated fix by MOMBot Pro\n\n" +
                    $"## Testing\n- [ ] Unit tests pass\n- [ ] Manual testing done",
            head  = branchName,
            @base = "Uat1",          // ← always target Uat1
            draft = false
        };

        var res = await _http.PostAsJsonAsync($"/repos/{_owner}/{_repo}/pulls", payload);
        var raw = await res.Content.ReadAsStringAsync();

        _logger.LogInformation("CreatePR {Status}: {Preview}",
            (int)res.StatusCode, raw[..Math.Min(300, raw.Length)]);

        if (!res.IsSuccessStatusCode)
        {
            if (raw.Contains("already exists"))
            {
                _logger.LogInformation("PR already exists");
                return new GitHubResult
                {
                    BranchName = branchName,
                    PRTitle    = $"[{jiraKey}] {title}",
                    PRUrl      = $"https://github.com/{_owner}/{_repo}/pulls",
                    Status     = "PR already exists"
                };
            }
            throw new Exception($"CreatePR failed {res.StatusCode}: {raw}");
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var prNumber = root.TryGetProperty("number",   out var num) ? num.GetInt32()     : 0;
        var prUrl    = root.TryGetProperty("html_url", out var url) ? url.GetString() ?? "" : "";

        _logger.LogInformation("PR #{N} created: {Url}", prNumber, prUrl);

        return new GitHubResult
        {
            BranchName    = branchName,
            PRNumber      = prNumber.ToString(),
            PRTitle       = $"[{jiraKey}] {title}",
            PRUrl         = prUrl,
            PRDescription = description,
            Status        = "PR raised → Uat1"
        };
    }
}
