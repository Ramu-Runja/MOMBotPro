using System.Diagnostics;
using System.Net.Http.Headers;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace MOMBotPro.API.Services;

public class AudioChunkingService
{
    private readonly ILogger<AudioChunkingService> _logger;
    private readonly IConfiguration               _config;

    private const int  CHUNK_SECONDS = 600;               // 10-min segments
    private const long SMALL_FILE    = 20 * 1024 * 1024;  // 20 MB threshold for direct Whisper

    // FFmpeg binaries downloaded once to a shared persistent location
    private static readonly string FfmpegDir =
        Path.Combine(Path.GetTempPath(), "mombot_ffmpeg");

    public AudioChunkingService(
        ILogger<AudioChunkingService> logger,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    // ── Primary entry point — handles URLs and local files of any size ────
    // source     : HTTPS URL  OR  local file path
    // zoomToken  : OAuth access token for Zoom download URLs (null for Recall.ai S3 URLs)
    // onProgress : callback invoked with "Transcribing chunk N/Total..." messages
    public async Task<string> ChunkAndTranscribeAsync(
        string          source,
        string          fileExt          = "mp4",
        string?         zoomToken        = null,
        Action<string>? onProgress       = null,
        CancellationToken ct             = default)
    {
        var openAiKey = _config["OpenAI:ApiKey"]
            ?? throw new Exception("OpenAI API key not configured");

        bool isUrl = source.StartsWith("http", StringComparison.OrdinalIgnoreCase);

        // Local file that fits in Whisper's 25 MB limit → send directly
        if (!isUrl && File.Exists(source))
        {
            var size = new FileInfo(source).Length;
            if (size < SMALL_FILE)
            {
                _logger.LogInformation("Local file {MB}MB — direct Whisper", size / 1024 / 1024);
                var bytes = await File.ReadAllBytesAsync(source, ct);
                return await SendToWhisper(bytes, fileExt, openAiKey);
            }
        }

        // URL or large local file → stream through FFmpeg segment muxer
        var tempDir = Path.Combine(Path.GetTempPath(), $"mombot_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        _logger.LogInformation("Chunk work dir: {Dir}", tempDir);

        try
        {
            var ffmpegDir = await EnsureFFmpegAsync(ct);

            // Probe duration upfront so we can report "chunk N/Total"
            double durationSec = await ProbeAudioDurationAsync(source, zoomToken, ffmpegDir, ct);
            int    estChunks   = durationSec > 0
                ? (int)Math.Ceiling(durationSec / CHUNK_SECONDS)
                : 0;

            _logger.LogInformation(
                "Source: {Source} | Duration: {Sec:F0}s | Est chunks: {N}",
                isUrl ? "(URL)" : source, durationSec, estChunks);

            onProgress?.Invoke("Extracting audio and splitting into 10-minute chunks...");

            // FFmpeg: stream source → extract audio → split into mp3 chunks
            // Never stores the full file — processes in a pipeline
            var chunkPattern = Path.Combine(tempDir, "chunk_%03d.mp3");
            await RunFFmpegSegmentAsync(source, zoomToken, chunkPattern, ffmpegDir, ct);

            var chunkFiles = Directory
                .GetFiles(tempDir, "chunk_*.mp3")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (chunkFiles.Count == 0)
            {
                _logger.LogWarning("FFmpeg produced no chunks from: {Source}", isUrl ? "(URL)" : source);
                return "";
            }

            int total = chunkFiles.Count;
            _logger.LogInformation("Transcribing {N} chunks in parallel (max 5 concurrent)...", total);

            var semaphore = new SemaphoreSlim(5);
            var tasks = chunkFiles.Select(async (chunkFile, index) =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    int n = index + 1;
                    onProgress?.Invoke($"Transcribing chunk {n}/{total} (parallel)...");
                    _logger.LogInformation("Chunk {N}/{Total}: {File}", n, total, Path.GetFileName(chunkFile));

                    var chunkBytes = await File.ReadAllBytesAsync(chunkFile, ct);
                    _logger.LogInformation("  Size: {MB:F1} MB", chunkBytes.Length / 1024.0 / 1024.0);

                    var text = await SendToWhisper(chunkBytes, "mp3", openAiKey);
                    return (index, text: string.IsNullOrWhiteSpace(text) ? "" : text.Trim());
                }
                finally
                {
                    semaphore.Release();
                    try { File.Delete(chunkFile); } catch { }
                }
            });

            var results = await Task.WhenAll(tasks);
            var transcript = string.Join("\n", results
                .OrderBy(r => r.index)
                .Select(r => r.text)
                .Where(t => t.Length > 0));

            _logger.LogInformation(
                "Transcription complete: {Chars} chars from {N} chunks", transcript.Length, total);
            return transcript;
        }
        finally
        {
            // Always clean up the temp directory, even on failure
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    // ── Legacy entry point — accepts raw bytes (kept for backward compat) ──
    public async Task<string> TranscribeAudio(byte[] fileBytes, string fileExt = "mp4")
    {
        var openAiKey = _config["OpenAI:ApiKey"]
            ?? throw new Exception("OpenAI API key not configured");

        _logger.LogInformation("TranscribeAudio: {MB}MB .{Ext}",
            fileBytes.Length / 1024 / 1024, fileExt);

        // Small enough for a single Whisper call
        if (fileBytes.Length <= SMALL_FILE)
        {
            _logger.LogInformation("File small enough — direct Whisper");
            return await SendToWhisper(fileBytes, fileExt, openAiKey);
        }

        // Write to temp file, then use streaming chunk path
        _logger.LogInformation("Large file ({MB}MB) — writing to temp then chunking",
            fileBytes.Length / 1024 / 1024);

        var tempFile = Path.Combine(Path.GetTempPath(), $"mombot_in_{Guid.NewGuid():N}.{fileExt}");
        try
        {
            await File.WriteAllBytesAsync(tempFile, fileBytes);
            return await ChunkAndTranscribeAsync(tempFile, fileExt);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    // ── FFmpeg: stream source → segment into mp3 chunks ──────────────────
    // Uses -f segment so FFmpeg never writes a full intermediate file.
    // For URLs: FFmpeg fetches and pipes in real time.
    // -ab 64k is sufficient for speech and keeps chunks tiny (~4.8 MB each).
    private async Task RunFFmpegSegmentAsync(
        string source,
        string? zoomToken,
        string chunkPattern,
        string ffmpegDir,
        CancellationToken ct)
    {
        var args = new List<string> { "-y" };  // overwrite without prompting

        // Auth header for Zoom direct downloads (Recall.ai S3 URLs are pre-signed)
        if (!string.IsNullOrEmpty(zoomToken))
        {
            args.Add("-headers");
            args.Add($"Authorization: Bearer {zoomToken}\r\n");
        }

        args.AddRange(new[]
        {
            "-i",               source,
            "-vn",                          // drop video stream — audio only
            "-acodec",          "mp3",
            "-ab",              "64k",      // 64 kbps — ideal for speech transcription
            "-f",               "segment",
            "-segment_time",    CHUNK_SECONDS.ToString(),
            "-reset_timestamps","1",
            chunkPattern,
        });

        _logger.LogInformation(
            "FFmpeg segment: source={S} pattern={P}",
            source.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? "(URL)" : source,
            Path.GetFileName(chunkPattern));

        await RunProcessAsync(GetFFmpegPath(ffmpegDir), args, ct);
    }

    // ── ffprobe: get total duration (seconds) so we can report progress ───
    private async Task<double> ProbeAudioDurationAsync(
        string source,
        string? zoomToken,
        string ffmpegDir,
        CancellationToken ct)
    {
        try
        {
            var args = new List<string>();

            if (!string.IsNullOrEmpty(zoomToken))
            {
                args.Add("-headers");
                args.Add($"Authorization: Bearer {zoomToken}\r\n");
            }

            args.AddRange(new[]
            {
                "-v",             "quiet",
                "-show_entries",  "format=duration",
                "-of",            "csv=p=0",
                source,
            });

            var output = await RunProcessOutputAsync(GetFFprobePath(ffmpegDir), args, ct);

            if (double.TryParse(
                    output.Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var secs))
                return secs;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("ffprobe duration failed: {Msg}", ex.Message);
        }

        return 0;
    }

    // ── Whisper API call — sends one mp3 chunk ────────────────────────────
    private async Task<string> SendToWhisper(byte[] bytes, string ext, string openAiKey)
    {
        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", openAiKey);

            using var form = new MultipartFormDataContent();

            var mimeType = ext switch
            {
                "mp3"  => "audio/mpeg",
                "m4a"  => "audio/m4a",
                "webm" => "audio/webm",
                "wav"  => "audio/wav",
                _      => "video/mp4",
            };

            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

            form.Add(content, "file", $"audio.{ext}");
            form.Add(new StringContent("whisper-1"), "model");
            form.Add(new StringContent("text"),      "response_format");
            form.Add(new StringContent("en"),        "language");

            var res = await client.PostAsync(
                "https://api.openai.com/v1/audio/transcriptions", form);
            var raw = await res.Content.ReadAsStringAsync();

            _logger.LogInformation("Whisper {Status}: {Preview}",
                (int)res.StatusCode,
                raw.Length > 300 ? raw[..300] : raw);

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogError("Whisper error {Status}: {Raw}", (int)res.StatusCode, raw);
                return "";
            }

            return raw.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError("SendToWhisper error: {Msg}", ex.Message);
            return "";
        }
    }

    // ── FFmpeg binary management ──────────────────────────────────────────

    private async Task<string> EnsureFFmpegAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(FfmpegDir);

        var ffmpegExe = GetFFmpegPath(FfmpegDir);
        if (!File.Exists(ffmpegExe))
        {
            _logger.LogInformation("FFmpeg not found — downloading to {Dir}...", FfmpegDir);
            FFmpeg.SetExecutablesPath(FfmpegDir);
            await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, FfmpegDir);
            _logger.LogInformation("FFmpeg downloaded.");
        }
        else
        {
            FFmpeg.SetExecutablesPath(FfmpegDir);
        }

        return FfmpegDir;
    }

    private static string GetFFmpegPath(string dir) =>
        Path.Combine(dir, OperatingSystem.IsWindows() ? "ffmpeg.exe"  : "ffmpeg");

    private static string GetFFprobePath(string dir) =>
        Path.Combine(dir, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

    // ── Process helpers ───────────────────────────────────────────────────

    private async Task RunProcessAsync(
        string              exe,
        IEnumerable<string> args,
        CancellationToken   ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)
            ?? throw new Exception($"Failed to start process: {exe}");

        // Must read both streams concurrently to avoid deadlocking the process
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);

        await proc.WaitForExitAsync(ct);

        var stderr = await stderrTask;
        await stdoutTask;

        // Log the tail of stderr (FFmpeg writes progress there)
        if (!string.IsNullOrEmpty(stderr))
            _logger.LogDebug("FFmpeg stderr: {Tail}",
                stderr.Length > 2000 ? stderr[^2000..] : stderr);

        if (proc.ExitCode != 0)
            throw new Exception(
                $"{Path.GetFileName(exe)} exited with code {proc.ExitCode}. " +
                $"Stderr tail: {(stderr.Length > 500 ? stderr[^500..] : stderr)}");
    }

    private async Task<string> RunProcessOutputAsync(
        string              exe,
        IEnumerable<string> args,
        CancellationToken   ct)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var proc = Process.Start(psi)
            ?? throw new Exception($"Failed to start process: {exe}");

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        await proc.WaitForExitAsync(ct);

        await stderrTask;  // consume to avoid buffer deadlock
        return await stdoutTask;
    }
}
