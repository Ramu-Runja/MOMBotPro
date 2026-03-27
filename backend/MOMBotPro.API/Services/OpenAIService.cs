using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Services;

public class OpenAIService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(HttpClient http, IConfiguration config, ILogger<OpenAIService> logger)
    {
        _http   = http;
        _config = config;
        _logger = logger;
        _http.BaseAddress = new Uri("https://api.openai.com");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _config["OpenAI:ApiKey"]);
    }

    private async Task<string> Chat(string systemPrompt, string userPrompt, bool jsonMode = false)
    {
        object body = jsonMode
            ? new
            {
                model           = "gpt-4o",
                messages        = new[] { new { role = "system", content = systemPrompt }, new { role = "user", content = userPrompt } },
                max_tokens      = 2000,
                temperature     = 0.3,
                response_format = new { type = "json_object" }
            }
            : new
            {
                model       = "gpt-4o",
                messages    = new[] { new { role = "system", content = systemPrompt }, new { role = "user", content = userPrompt } },
                max_tokens  = 2000,
                temperature = 0.3
            };

        var json    = JsonSerializer.Serialize(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/v1/chat/completions", content);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("OpenAI API error {Status}: {Body}", response.StatusCode, errorBody);
            throw new Exception($"OpenAI API error: {(int)response.StatusCode} - {errorBody}");
        }

        var raw = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "";
    }

    // Step 1: Extract MOM + bug summary from Tenglish transcript
    public async Task<(string mom, string bugSummary)> ExtractFromTranscript(string transcript, string clientName)
    {
        var system = "You are MOMBot Pro — an AI assistant that analyzes Telugu+English (Tenglish) meeting transcripts. Extract structured meeting information accurately.";

        var user = "Analyze this Tenglish client call transcript for client " + clientName + ".\n\n" +
                   "Extract and return ONLY a JSON object:\n" +
                   "{\n" +
                   "  \"momSummary\": \"detailed minutes of meeting summary\",\n" +
                   "  \"bugDescription\": \"specific bug or issue mentioned\",\n" +
                   "  \"priority\": \"Critical/High/Medium/Low\",\n" +
                   "  \"actionItems\": [\"item1\", \"item2\"],\n" +
                   "  \"attendees\": [\"name1\", \"name2\"]\n" +
                   "}\n\n" +
                   "Transcript:\n" + transcript;

        var text = await Chat(system, user, jsonMode: true);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var mom = root.TryGetProperty("momSummary",    out var m) ? m.GetString() ?? "" : "";
        var bug = root.TryGetProperty("bugDescription", out var b) ? b.GetString() ?? "" : "";
        return (mom, bug);
    }

    // Step 2: Analyze codebase files and find the bug
    public async Task<BugAnalysis> AnalyzeBug(string bugDescription, string codebaseContext)
    {
        var system = "You are an expert C# .NET developer analyzing a bug in GlowCart, a shopping application. " +
                     "Return ONLY valid JSON with no markdown fences.";

        var user = "Bug report: " + bugDescription + "\n\n" +
                   "Codebase context:\n" + codebaseContext + "\n\n" +
                   "Analyze the bug and return ONLY valid JSON (no markdown):\n" +
                   "{\n" +
                   "  \"bugDescription\": \"one line description\",\n" +
                   "  \"fileName\": \"relative/path/to/file.cs\",\n" +
                   "  \"lineNumber\": 42,\n" +
                   "  \"rootCause\": \"explanation of root cause\",\n" +
                   "  \"originalCode\": \"the buggy code snippet\",\n" +
                   "  \"fixedCode\": \"the COMPLETE corrected file content\",\n" +
                   "  \"suggestedFix\": \"explanation of the fix\"\n" +
                   "}\n\n" +
                   "IMPORTANT:\n" +
                   "- fileName must be the actual relative file path (e.g. GlowCart.BLL/Services/OrderService.cs)\n" +
                   "- fixedCode must be the COMPLETE corrected file content, not just a snippet\n" +
                   "- If you cannot find the exact file, use the most likely file based on the bug description";

        var text  = await Chat(system, user, jsonMode: true);
        var clean = text.Replace("```json", "").Replace("```", "").Trim();
        return JsonSerializer.Deserialize<BugAnalysis>(clean, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new BugAnalysis();
    }

    // Step 3: Generate PR description
    public async Task<string> GeneratePRDescription(string bugDescription, BugAnalysis analysis)
    {
        var system = "You are a senior software engineer writing professional GitHub PR descriptions.";

        var user = "Write a professional GitHub PR description for this bug fix:\n" +
                   "Bug: " + bugDescription + "\n" +
                   "File: " + analysis.FileName + "\n" +
                   "Root cause: " + analysis.RootCause + "\n" +
                   "Fix: " + analysis.SuggestedFix + "\n\n" +
                   "Keep it under 150 words. Include: What changed, Why, How to test.";

        return await Chat(system, user);
    }
}
