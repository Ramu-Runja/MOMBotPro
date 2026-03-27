using MOMBotPro.API.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MOMBotPro.API.Services;

public partial class RecallService
{
    [GeneratedRegex(@"https?://[a-z0-9]+\.zoom\.us")]
    private static partial Regex RegionalSubdomainRegex();

    [GeneratedRegex(@"/j/(\d+)")]
    private static partial Regex MeetingIdRegex();

    [GeneratedRegex(@"[?&]pwd=([^&\s]+)")]
    private static partial Regex PwdParamRegex();

    [GeneratedRegex(@"\.\d+$")]
    private static partial Regex PwdVersionSuffixRegex();

    private readonly HttpClient             _http;
    private readonly IConfiguration        _config;
    private readonly ILogger<RecallService> _logger;
    private readonly AudioChunkingService  _chunker;

    private string Region => _config["Recall:Region"] ?? "us-west-2";

    public RecallService(
        HttpClient http,
        IConfiguration config,
        ILogger<RecallService> logger,
        AudioChunkingService chunker)
    {
        _http    = http;
        _config  = config;
        _logger  = logger;
        _chunker = chunker;

        _http.BaseAddress = new Uri($"https://{Region}.recall.ai/api/v1/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Token", config["Recall:ApiKey"]);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // ── CreateBot — absolute minimum payload, audio transcription only ──
    public async Task<string> CreateBot(string meetingUrl)
    {
        var cleanUrl = CleanZoomUrl(meetingUrl);

        _logger.LogInformation("Sending to Recall.ai — URL: {Url}, BotName: MOMBot Pro", cleanUrl);

        var payload = new
        {
            meeting_url = cleanUrl,
            bot_name    = "ramurunja91@gmail.com"
        };

        var json     = JsonSerializer.Serialize(payload);
        var response = await _http.PostAsync("bot/",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var raw = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("Recall CreateBot: {Status} - {Raw}",
            (int)response.StatusCode, raw);

        if (!response.IsSuccessStatusCode)
            throw new Exception($"Recall.ai error ({(int)response.StatusCode}): {raw}");

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("id").GetString()
               ?? throw new Exception("Recall.ai returned no bot ID.");
    }

    // ── Clean URL — normalises regional subdomains, paths, and pwd param ──
    private string CleanZoomUrl(string url)
    {
        try
        {
            _logger.LogInformation("Original URL: {Url}", url);

            // Fix regional subdomains: us05web.zoom.us → zoom.us
            url = RegionalSubdomainRegex().Replace(url, "https://zoom.us");

            // Extract meeting ID from /j/XXXXXXXXX
            var meetingId = MeetingIdRegex().Match(url).Groups[1].Value;

            // Extract pwd and remove trailing .N (e.g. .1)
            var pwdMatch = PwdParamRegex().Match(url);
            var pwd      = pwdMatch.Success ? pwdMatch.Groups[1].Value : "";
            pwd = PwdVersionSuffixRegex().Replace(pwd, "");

            if (!string.IsNullOrEmpty(meetingId))
            {
                var clean = string.IsNullOrEmpty(pwd)
                    ? $"https://zoom.us/j/{meetingId}"
                    : $"https://zoom.us/j/{meetingId}?pwd={pwd}";
                _logger.LogInformation("Cleaned URL: {Url}", clean);
                return clean;
            }

            _logger.LogWarning("Could not extract meeting ID from URL: {Url}", url);
            return url;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("CleanZoomUrl error: {Msg}", ex.Message);
            return url;
        }
    }

    // ── GetBotStatus — returns current status code string ────────────────
    public async Task<string> GetBotStatus(string botId)
    {
        var res = await _http.GetAsync($"bot/{botId}/");
        var raw = await res.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        // Read latest from status_changes
        if (root.TryGetProperty("status_changes", out var sc) && sc.GetArrayLength() > 0)
        {
            var last = sc[sc.GetArrayLength() - 1];
            if (last.TryGetProperty("code", out var code))
            {
                var s = code.GetString() ?? "joining";
                _logger.LogInformation("Bot {Id} → {S}", botId, s);
                return s;
            }
        }

        // Fallback: recording with status=done means call ended
        if (root.TryGetProperty("recordings", out var recs) &&
            recs.GetArrayLength() > 0 &&
            recs[0].TryGetProperty("status", out var rs) &&
            rs.TryGetProperty("code", out var rc) &&
            rc.GetString() == "done")
            return "call_ended";

        return "joining";
    }

    // ── GetTranscript — stream from Recall.ai → FFmpeg → Whisper ─────────
    // Never downloads the full file — FFmpeg streams directly from the URL,
    // extracts audio, and splits into 10-min chunks on the fly.
    public async Task<string> GetTranscript(
        string          botId,
        Action<string>? onProgress = null,
        CancellationToken ct       = default)
    {
        try
        {
            _logger.LogInformation("GetTranscript for bot {Id}", botId);

            var botRes = await _http.GetAsync($"bot/{botId}/");
            var botRaw = await botRes.Content.ReadAsStringAsync();
            _logger.LogInformation("Bot response: {Raw}", botRaw);

            using var botDoc = JsonDocument.Parse(botRaw);
            var botRoot = botDoc.RootElement;

            if (!botRoot.TryGetProperty("recordings", out var recs) || recs.GetArrayLength() == 0)
            {
                _logger.LogWarning("No recordings yet for bot {Id}", botId);
                return "";
            }

            var rec = recs[0];

            // Only proceed if recording is fully done
            if (rec.TryGetProperty("status", out var recStatus) &&
                recStatus.TryGetProperty("code", out var recCode))
            {
                var code = recCode.GetString();
                _logger.LogInformation("Recording status: {S}", code);
                if (code != "done")
                {
                    _logger.LogInformation("Recording not done yet ({S})", code);
                    return "";
                }
            }

            string? downloadUrl = null;
            string  fileExt     = "mp4";

            if (rec.TryGetProperty("media_shortcuts", out var ms))
            {
                // Prefer audio_mixed (smaller file)
                if (ms.TryGetProperty("audio_mixed", out var am) &&
                    am.ValueKind != JsonValueKind.Null &&
                    am.TryGetProperty("data", out var amd) &&
                    amd.ValueKind != JsonValueKind.Null &&
                    amd.TryGetProperty("download_url", out var adu) &&
                    adu.ValueKind != JsonValueKind.Null)
                {
                    downloadUrl = adu.GetString();
                    fileExt     = "m4a";
                    _logger.LogInformation("Using audio_mixed");
                }

                // Fallback to video_mixed (confirmed to exist)
                if (string.IsNullOrEmpty(downloadUrl) &&
                    ms.TryGetProperty("video_mixed", out var vm) &&
                    vm.ValueKind != JsonValueKind.Null &&
                    vm.TryGetProperty("data", out var vmd) &&
                    vmd.ValueKind != JsonValueKind.Null &&
                    vmd.TryGetProperty("download_url", out var vdu) &&
                    vdu.ValueKind != JsonValueKind.Null)
                {
                    downloadUrl = vdu.GetString();
                    fileExt     = "mp4";
                    _logger.LogInformation("Using video_mixed");
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                _logger.LogWarning("No download URL found for bot {Id}", botId);
                return "";
            }

            _logger.LogInformation(
                "Streaming {Ext} via FFmpeg (no full download) — supports 7GB+ files", fileExt);

            // Recall.ai S3 URLs are pre-signed — no auth header needed.
            // FFmpeg streams directly from the URL, extracts audio at 64kbps,
            // and writes 10-minute mp3 chunks. The full video is never stored.
            return await _chunker.ChunkAndTranscribeAsync(
                downloadUrl,
                fileExt,
                zoomToken:  null,
                onProgress: onProgress,
                ct:         ct);
        }
        catch (Exception ex)
        {
            _logger.LogError("GetTranscript error: {Msg}", ex.Message);
            return "";
        }
    }

    // ── WaitForTranscript — polls until terminal status, then transcribes ─
    public async Task<string> WaitForTranscript(
        string            botId,
        IProgress<string>? progress  = null,
        CancellationToken  ct        = default)
    {
        var timeout = DateTime.UtcNow.AddHours(4);

        while (DateTime.UtcNow < timeout)
        {
            ct.ThrowIfCancellationRequested();
            var status = await GetBotStatus(botId);
            _logger.LogInformation("Bot {Id} → {S}", botId, status);
            progress?.Report(status);

            switch (status)
            {
                case "in_waiting_room":
                case "joining_call":
                    break;

                case "in_call_not_recording":
                case "in_call_recording":
                    progress?.Report("in_call");
                    break;

                case "call_ended":
                case "done":
                case "recording_done":
                case "completed":
                    _logger.LogInformation("Call ended! Waiting 45s for S3 upload...");
                    progress?.Report("processing");
                    await Task.Delay(45_000);

                    for (int i = 0; i < 5; i++)
                    {
                        _logger.LogInformation("Attempt {N}/5...", i + 1);
                        var t = await GetTranscript(
                            botId,
                            onProgress: msg => progress?.Report(msg),
                            ct: ct);
                        if (!string.IsNullOrEmpty(t)) return t;
                        if (i < 4)
                        {
                            _logger.LogInformation("Not ready. Waiting 30s...");
                            await Task.Delay(30_000, ct);
                        }
                    }

                    _logger.LogWarning("All attempts failed");
                    return "";

                case "media_expired":
                    return await GetTranscript(botId, msg => progress?.Report(msg), ct);

                case "fatal":
                case "error":
                case "kicked":
                case "failed":
                    throw new Exception($"Bot failed with status: {status}");
            }

            await Task.Delay(8_000, ct);
        }

        throw new Exception("Timeout after 4 hours");
    }

    // ── GetBotDebugInfo — raw recording/media_shortcuts info for debugging ──
    public async Task<object> GetBotDebugInfo(string botId)
    {
        var res = await _http.GetAsync($"bot/{botId}/");
        var raw = await res.Content.ReadAsStringAsync();

        using var doc  = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var statusCode = "unknown";
        if (root.TryGetProperty("status_changes", out var sc) && sc.GetArrayLength() > 0)
            statusCode = sc[sc.GetArrayLength() - 1].GetProperty("code").GetString() ?? "unknown";

        var recCount     = root.TryGetProperty("recordings", out var recs) ? recs.GetArrayLength() : 0;
        var mediaShortcuts = recCount > 0 && recs[0].TryGetProperty("media_shortcuts", out var ms)
            ? ms.ToString() : "none";

        return new { status = statusCode, recordings_count = recCount, media_shortcuts = mediaShortcuts };
    }

    // ── GetRecordingUrls — audio-only, tries all known shapes ────────────
    public async Task<RecordingInfo> GetRecordingUrls(string botId)
    {
        var res = await _http.GetAsync($"bot/{botId}/");
        var raw = await res.Content.ReadAsStringAsync();
        _logger.LogInformation("Recall bot response for {BotId}: {Raw}", botId, raw);

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var audioUrl = "";

        // 1. Top-level audio_url
        if (root.TryGetProperty("audio_url", out var a) && a.ValueKind != JsonValueKind.Null)
            audioUrl = a.GetString() ?? "";

        // 2. recording_files[] — prefer audio_only
        if (string.IsNullOrEmpty(audioUrl) &&
            root.TryGetProperty("recording_files", out var files) &&
            files.ValueKind == JsonValueKind.Array)
        {
            foreach (var file in files.EnumerateArray())
            {
                if (!file.TryGetProperty("recording_type", out var rt)) continue;
                var type = rt.GetString() ?? "";
                if (type is "audio_only" &&
                    file.TryGetProperty("download_url", out var al))
                    audioUrl = al.GetString() ?? "";
            }
        }

        // 3. media_shortcuts.audio_mixed
        if (string.IsNullOrEmpty(audioUrl) &&
            root.TryGetProperty("media_shortcuts", out var shortcuts) &&
            shortcuts.TryGetProperty("audio_mixed", out var am) &&
            am.TryGetProperty("data", out var amData) &&
            amData.TryGetProperty("download_url", out var amUrl))
        {
            audioUrl = amUrl.GetString() ?? "";
        }

        _logger.LogInformation("Recording audio URL for {BotId}: {Audio}", botId, audioUrl);

        return new RecordingInfo
        {
            VideoUrl  = "",
            AudioUrl  = audioUrl,
            ExpiresAt = DateTime.UtcNow.AddHours(6)
        };
    }
}
