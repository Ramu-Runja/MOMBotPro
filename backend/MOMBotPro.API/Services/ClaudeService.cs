using System.Text;
using System.Text.Json;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Services;

public class ClaudeService
{
    private readonly HttpClient    _http;
    private readonly IConfiguration _config;
    private readonly ILogger<ClaudeService> _logger;

    public ClaudeService(HttpClient http, IConfiguration config, ILogger<ClaudeService> logger)
    {
        _http   = http;
        _config = config;
        _logger = logger;
        _http.BaseAddress = new Uri("https://api.anthropic.com");
        _http.DefaultRequestHeaders.Add("x-api-key",          _config["Anthropic:ApiKey"]);
        _http.DefaultRequestHeaders.Add("anthropic-version",  "2023-06-01");
    }

    private async Task<string> Ask(string prompt)
    {
        var body = new
        {
            model      = "claude-sonnet-4-6",
            max_tokens = 2048,
            messages   = new[] { new { role = "user", content = prompt } }
        };

        var json    = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/v1/messages", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("Anthropic API error {Status}: {Body}", response.StatusCode, errorBody);
            throw new Exception($"Anthropic API error: {errorBody}");
        }

        var raw = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
    }

    // Step 1: Extract MOM + bug summary from transcript
    public async Task<(string mom, string bugSummary)> ExtractFromTranscript(string transcript, string clientName)
    {
        var prompt = $$"""
            This is a Telugu-English meeting transcript with client {{clientName}}.
            Transcript: {{transcript}}

            Respond ONLY with JSON:
            {
              "mom": "2-3 sentence meeting summary in English",
              "bugSummary": "Clear description of the bug reported. Include: what is broken, where it happens, what the expected behavior is."
            }
            """;

        var text  = await Ask(prompt);
        var clean = text.Replace("```json", "").Replace("```", "").Trim();
        using var doc = JsonDocument.Parse(clean);
        var mom = doc.RootElement.GetProperty("mom").GetString() ?? "";
        var bug = doc.RootElement.GetProperty("bugSummary").GetString() ?? "";
        return (mom, bug);
    }

    // Step 2: Analyze codebase files and find the bug
    public async Task<BugAnalysis> AnalyzeBug(string bugSummary, string codebaseContext)
    {
        var prompt = $$"""
            Bug reported: {{bugSummary}}

            Codebase files:
            {{codebaseContext}}

            Analyze and respond ONLY with JSON:
            {
              "fileName": "most likely file with the bug",
              "lineNumber": 42,
              "bugDescription": "what exactly is wrong",
              "rootCause": "why this bug happens",
              "suggestedFix": "plain English explanation of the fix",
              "originalCode": "the buggy code snippet",
              "fixedCode": "the corrected code snippet"
            }
            """;

        var text  = await Ask(prompt);
        var clean = text.Replace("```json", "").Replace("```", "").Trim();
        return JsonSerializer.Deserialize<BugAnalysis>(clean, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new BugAnalysis();
    }

    // Step 3: Generate PR description
    public async Task<string> GeneratePRDescription(string bugSummary, BugAnalysis analysis)
    {
        var prompt = $"""
            Write a professional GitHub PR description for this bug fix:
            Bug: {bugSummary}
            File: {analysis.FileName}
            Root cause: {analysis.RootCause}
            Fix: {analysis.SuggestedFix}

            Keep it under 150 words. Include: What changed, Why, How to test.
            """;
        return await Ask(prompt);
    }
}
