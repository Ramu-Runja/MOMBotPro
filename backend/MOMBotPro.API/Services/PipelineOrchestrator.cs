using System.Diagnostics;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Controllers;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Services;

public class PipelineOrchestrator
{
    private static readonly ActivitySource _activitySource = new("MOMBotPro.Pipeline");

    private readonly PipelineRepository    _repo;
    private readonly OpenAIService         _openAI;
    private readonly IJiraService          _jira;
    private readonly GitHubService         _github;
    private readonly RecallService         _recall;
    private readonly IHttpClientFactory    _httpFactory;
    private readonly IConfiguration        _config;
    private readonly ApplicationDbContext  _db;
    private readonly SlackService          _slack;
    private readonly ITokenProvider        _tokenProvider;
    private readonly ILogger<PipelineOrchestrator> _logger;

    public PipelineOrchestrator(
        PipelineRepository   repo,
        OpenAIService        openAI,
        IJiraService         jira,
        GitHubService        github,
        RecallService        recall,
        IHttpClientFactory   httpFactory,
        IConfiguration       config,
        ApplicationDbContext db,
        SlackService         slack,
        ITokenProvider       tokenProvider,
        ILogger<PipelineOrchestrator> logger)
    {
        _repo          = repo;
        _openAI        = openAI;
        _jira          = jira;
        _github        = github;
        _recall        = recall;
        _httpFactory   = httpFactory;
        _config        = config;
        _db            = db;
        _slack         = slack;
        _tokenProvider = tokenProvider;
        _logger        = logger;
    }

    // Syncs the in-memory step so Save() writes the correct status, then persists.
    // Without this sync, Save(pipeline) would overwrite DB steps back to "Waiting"
    // because the in-memory pipeline.Steps are never updated by UpdateStep() alone.
    private void ApplyStep(Pipeline pipeline, string pipelineId, string stepName,
                           StepStatus status, string? message = null)
    {
        var step = pipeline.Steps.FirstOrDefault(s => s.Name == stepName);
        if (step != null)
        {
            step.Status  = status;
            step.Message = message;
            if (status is StepStatus.Done or StepStatus.Failed)
                step.CompletedAt = DateTime.UtcNow;
        }
        _repo.UpdateStep(pipelineId, stepName, status, message);
        _repo.Save(pipeline);
    }

    public async Task RunAsync(
        string  pipelineId,
        string  transcript,
        string  clientName,
        string? botId  = null,
        Guid?   userId = null)
    {
        var pipeline = _repo.Get(pipelineId);
        if (pipeline == null) return;
        pipeline.Status = PipelineStatus.Running;
        _repo.Save(pipeline);

        // ── Load integrations from DB and configure real services ────────
        var uid = userId ?? pipeline.UserId;
        // Treat Guid.Empty (unset) as null so we fall back to any-user query
        if (uid == Guid.Empty) uid = null;

        _logger.LogInformation("Pipeline {Id} userId={Uid}", pipelineId, uid?.ToString() ?? "null");

        var jiraInteg = uid.HasValue
            ? await _db.Integrations.FirstOrDefaultAsync(i =>
                i.UserId == uid.Value && i.Type.ToLower() == "jira" && i.IsConnected)
            : await _db.Integrations.FirstOrDefaultAsync(i =>
                i.Type.ToLower() == "jira" && i.IsConnected);

        if (jiraInteg != null)
        {
            if (string.IsNullOrEmpty(jiraInteg.Domain) &&
                IntegrationController.BackfillFromConfigJson(jiraInteg, _logger))
                await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(jiraInteg.Domain))
            {
                _jira.Configure(
                    jiraInteg.Domain,
                    jiraInteg.Email       ?? "",
                    jiraInteg.AccessToken ?? "",
                    jiraInteg.ProjectKey  ?? "",
                    jiraInteg.AccountId,
                    uid);
                _logger.LogInformation("Jira configured: {Domain}/{Key}", jiraInteg.Domain, jiraInteg.ProjectKey);
            }
            else
            {
                _logger.LogWarning("Jira row found but Domain is empty (userId={Uid})", uid);
            }
        }
        else
        {
            _logger.LogWarning("No Jira integration found in DB (userId={Uid})", uid);
        }

        var ghInteg = uid.HasValue
            ? await _db.Integrations.FirstOrDefaultAsync(i =>
                i.UserId == uid.Value && i.Type.ToLower() == "github" && i.IsConnected)
            : await _db.Integrations.FirstOrDefaultAsync(i =>
                i.Type.ToLower() == "github" && i.IsConnected);

        if (ghInteg != null)
        {
            if (string.IsNullOrEmpty(ghInteg.AccessToken) &&
                IntegrationController.BackfillFromConfigJson(ghInteg, _logger))
                await _db.SaveChangesAsync();

            if (!string.IsNullOrEmpty(ghInteg.AccessToken) || uid.HasValue)
            {
                if (uid.HasValue)
                    _github.Configure(ghInteg.Owner ?? "Ramu-9346", ghInteg.Repo ?? "GlowCart", uid.Value);
                else
                    _github.Configure(ghInteg.Owner ?? "Ramu-9346", ghInteg.Repo ?? "GlowCart", ghInteg.AccessToken!);
                _logger.LogInformation("GitHub configured: {Owner}/{Repo}", ghInteg.Owner, ghInteg.Repo);
            }
            else
            {
                _logger.LogWarning("GitHub row found but AccessToken is empty (userId={Uid})", uid);
            }
        }
        else
        {
            _logger.LogWarning("No GitHub integration found in DB — using appsettings defaults (userId={Uid})", uid);
        }

        // Variables that span multiple step scopes
        var bugSummary  = "";
        JiraTicket  ticket   = default!;
        BugAnalysis analysis = default!;

        try
        {
            // ── STEP 1: Transcribe Audio ──────────────────────────────────
            using (var activity = _activitySource.StartActivity("Transcribe Audio"))
            {
                activity?.SetTag("pipeline.id", pipelineId);
                activity?.SetTag("step.name", "Transcribe Audio");
                ApplyStep(pipeline, pipelineId, "Transcribe Audio", StepStatus.Running, "Processing audio...");
                await Task.Delay(500);
                pipeline.Transcript = transcript;

                if (!string.IsNullOrEmpty(botId))
                {
                    try
                    {
                        var recording = await _recall.GetRecordingUrls(botId);
                        pipeline.RecordingAudioUrl  = recording.AudioUrl;
                        pipeline.RecordingExpiresAt = recording.ExpiresAt;
                        pipeline.BotId              = botId;
                    }
                    catch (Exception ex)
                    {
                        pipeline.BotId = botId;
                        _logger.LogWarning("Recording URL fetch failed: {Msg}", ex.Message);
                    }
                }

                ApplyStep(pipeline, pipelineId, "Transcribe Audio", StepStatus.Done,
                    $"Transcript ready. {transcript.Length} characters.");
            }

            // ── STEP 2: Extract MOM + Bug (OpenAI) ───────────────────────
            using (var activity = _activitySource.StartActivity("Extract Bug from MOM"))
            {
                activity?.SetTag("pipeline.id", pipelineId);
                activity?.SetTag("step.name", "Extract Bug from MOM");
                ApplyStep(pipeline, pipelineId, "Extract Bug from MOM", StepStatus.Running,
                    "GPT-4o reading Tenglish transcript...");
                var (mom, extractedBug) = await _openAI.ExtractFromTranscript(transcript, clientName);
                pipeline.MOMSummary = mom;
                pipeline.BugSummary = extractedBug;
                bugSummary = extractedBug;
                ApplyStep(pipeline, pipelineId, "Extract Bug from MOM", StepStatus.Done,
                    "Bug: " + bugSummary[..Math.Min(bugSummary.Length, 80)] + "...");
            }

            // ── STEP 3: Create Jira Ticket ────────────────────────────────
            using (var activity = _activitySource.StartActivity("Create Jira Ticket"))
            {
                activity?.SetTag("pipeline.id", pipelineId);
                activity?.SetTag("step.name", "Create Jira Ticket");
                ApplyStep(pipeline, pipelineId, "Create Jira Ticket", StepStatus.Running,
                    "Creating ticket in Jira...");
                ticket = await _jira.CreateTicket(bugSummary, clientName, userId);
                pipeline.JiraTicket = ticket;
                ApplyStep(pipeline, pipelineId, "Create Jira Ticket", StepStatus.Done,
                    $"Ticket created: {ticket.Key}");
            }

            // ── STEP 4: Scan Codebase ─────────────────────────────────────
            using (var activity = _activitySource.StartActivity("Scan Codebase"))
            {
                activity?.SetTag("pipeline.id", pipelineId);
                activity?.SetTag("step.name", "Scan Codebase");
                ApplyStep(pipeline, pipelineId, "Scan Codebase", StepStatus.Running,
                    "Scanning GlowCart repo on GitHub...");
                var codebaseCtx = await _github.GetCodebaseContext(bugSummary);
                analysis        = await _openAI.AnalyzeBug(bugSummary, codebaseCtx);
                pipeline.BugAnalysis = analysis;
                ApplyStep(pipeline, pipelineId, "Scan Codebase", StepStatus.Done,
                    $"Bug found in: {analysis.FileName} at line {analysis.LineNumber}");
            }

            // ── STEP 5: Generate Fix ──────────────────────────────────────
            using (var activity = _activitySource.StartActivity("Generate Fix"))
            {
                activity?.SetTag("pipeline.id", pipelineId);
                activity?.SetTag("step.name", "Generate Fix");
                ApplyStep(pipeline, pipelineId, "Generate Fix", StepStatus.Running,
                    "GPT-4o generating C# fix...");
                // Fix is already in analysis.FixedCode / analysis.SuggestedFix from AnalyzeBug
                ApplyStep(pipeline, pipelineId, "Generate Fix", StepStatus.Done,
                    $"Fix ready for {analysis.FileName}");
            }

            // ── STEP 6: Create Branch from default branch & Raise PR → Uat1 ──
            using (var activity = _activitySource.StartActivity("Create Branch & Raise PR"))
            {
                activity?.SetTag("pipeline.id", pipelineId);
                activity?.SetTag("step.name", "Create Branch & Raise PR");
                ApplyStep(pipeline, pipelineId, "Create Branch & Raise PR", StepStatus.Running,
                    $"Creating branch for {ticket.Key}...");

                var branchName = await _github.CreateBranch(ticket.Key, analysis.BugDescription);

                if (!string.IsNullOrEmpty(analysis.FileName) &&
                    !string.IsNullOrEmpty(analysis.FixedCode))
                {
                    await _github.CommitFix(
                        branchName,
                        analysis.FileName,
                        analysis.FixedCode,
                        ticket.Key,
                        analysis.BugDescription);
                }

                var prTitle      = analysis.BugDescription.Length > 60
                    ? analysis.BugDescription[..60]
                    : analysis.BugDescription;
                var prDesc       = await _openAI.GeneratePRDescription(bugSummary, analysis);
                var githubResult = await _github.CreatePR(branchName, ticket.Key, prTitle, prDesc);
                pipeline.GitHubResult = githubResult;
                ApplyStep(pipeline, pipelineId, "Create Branch & Raise PR", StepStatus.Done,
                    $"PR raised → Uat1: {githubResult.PRUrl}");
            }

            // ── Slack notification (fire-and-forget, non-fatal) ───────────
            await SendSlackAlert(clientName, ticket, analysis, pipeline.GitHubResult!, pipelineId, uid);

            // ── Email notification (fire-and-forget, non-fatal) ───────────
            await SendPREmail(clientName, ticket, analysis, pipeline.GitHubResult!, uid);

            pipeline.Status = PipelineStatus.Done;
            _repo.Save(pipeline);
        }
        catch (Exception ex)
        {
            // Log full exception (not just Message) so stack trace is captured
            _logger.LogError("Pipeline {Id} failed: {Ex}", pipelineId, ex.ToString());
            pipeline.Status = PipelineStatus.Failed;

            // After ApplyStep fixes in-memory sync, this correctly finds the running step
            var running = pipeline.Steps.FirstOrDefault(s => s.Status == StepStatus.Running);
            if (running != null)
            {
                running.Status      = StepStatus.Failed;
                running.Message     = ex.Message;
                running.CompletedAt = DateTime.UtcNow;
                _repo.UpdateStep(pipelineId, running.Name, StepStatus.Failed, ex.Message);
            }
            _repo.Save(pipeline);
        }
    }

    private async Task SendSlackAlert(
        string clientName, JiraTicket ticket,
        BugAnalysis analysis, GitHubResult gh, string pipelineId,
        Guid? userId = null)
    {
        try
        {
            // Load Slack integration from DB (bot token takes priority over webhook)
            var slackInteg = userId.HasValue
                ? await _db.Integrations.FirstOrDefaultAsync(i =>
                    i.UserId == userId.Value && i.Type.ToLower() == "slack" && i.IsConnected)
                : await _db.Integrations.FirstOrDefaultAsync(i =>
                    i.Type.ToLower() == "slack" && i.IsConnected);

            var text = $"*New PR Raised!* — {clientName}";
            var blocks = new object[]
            {
                new {
                    type = "section",
                    text = new {
                        type = "mrkdwn",
                        text = $"*New PR Raised!* :tada:\n" +
                               $"*Client:* {clientName}\n" +
                               $"*Jira:* {ticket.Key} — {ticket.Summary}\n" +
                               $"*Bug:* {analysis.FileName}:{analysis.LineNumber}\n" +
                               $"*PR:* <{gh.PRUrl}|{gh.PRTitle}>"
                    }
                }
            };

            await _slack.SendAsync(slackInteg, text, blocks);
            _logger.LogInformation("Slack notification sent for pipeline {Id}", pipelineId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Slack notification failed: {Msg}", ex.Message);
        }
    }

    private async Task SendPREmail(
        string clientName, JiraTicket ticket,
        BugAnalysis analysis, GitHubResult gh,
        Guid? userId = null)
    {
        try
        {
            var gmailInteg = userId.HasValue
                ? await _db.Integrations.FirstOrDefaultAsync(i =>
                    i.UserId == userId.Value && i.Type.ToLower() == "gmail" && i.IsConnected)
                : await _db.Integrations.FirstOrDefaultAsync(i =>
                    i.Type.ToLower() == "gmail" && i.IsConnected);

            if (gmailInteg == null ||
                string.IsNullOrEmpty(gmailInteg.Email) ||
                string.IsNullOrEmpty(gmailInteg.AccessToken))
            {
                _logger.LogWarning("Gmail integration not configured — skipping PR email.");
                return;
            }

            var fromAddress  = gmailInteg.Email;
            var toAddress    = !string.IsNullOrEmpty(gmailInteg.Domain)
                               ? gmailInteg.Domain
                               : gmailInteg.Email;

            // Exchange the stored refresh token for a short-lived access token
            string? accessToken = null;
            if (userId.HasValue)
            {
                try { accessToken = await _tokenProvider.GetAccessTokenAsync("Gmail", userId.Value); }
                catch (Exception ex) { _logger.LogWarning("Gmail token fetch failed: {Msg}", ex.Message); }
            }
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogWarning("Gmail token exchange returned no access_token — skipping PR email.");
                return;
            }

            var subject = $"[MOMBotPro] New PR Raised - {ticket.Key}: {gh.PRTitle}";
            var body    = $"""
                <html><body style="font-family:Arial,sans-serif;line-height:1.6;">
                <p>Hi Team,</p>
                <p>A new Pull Request has been automatically raised by MOMBotPro.</p>
                <table cellpadding="6" style="border-collapse:collapse;">
                  <tr><td><strong>Client</strong></td><td>{clientName}</td></tr>
                  <tr><td><strong>Jira Ticket</strong></td>
                      <td><a href="{ticket.Url}">{ticket.Key}</a> — {ticket.Summary}</td></tr>
                  <tr><td><strong>Bug Found In</strong></td>
                      <td>{analysis.FileName} at line {analysis.LineNumber}</td></tr>
                  <tr><td><strong>Root Cause</strong></td><td>{analysis.RootCause}</td></tr>
                  <tr><td><strong>PR Title</strong></td><td>{gh.PRTitle}</td></tr>
                  <tr><td><strong>PR URL</strong></td>
                      <td><a href="{gh.PRUrl}">{gh.PRUrl}</a></td></tr>
                  <tr><td><strong>Branch</strong></td>
                      <td>{gh.BranchName} → Uat1</td></tr>
                </table>
                <br/>
                <p style="color:#888;font-size:12px;">This was auto-generated by MOMBotPro AI Pipeline.</p>
                </body></html>
                """;

            var mime = new MimeMessage();
            mime.From.Add(MailboxAddress.Parse(fromAddress));
            mime.To.Add(MailboxAddress.Parse(toAddress));
            mime.Subject = subject;
            mime.Body    = new TextPart("html") { Text = body };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(new SaslMechanismOAuth2(fromAddress, accessToken));
            await smtp.SendAsync(mime);
            await smtp.DisconnectAsync(quit: true);

            _logger.LogInformation("PR email sent to {To} for ticket {Key}", toAddress, ticket.Key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PR email notification failed: {Msg}", ex.Message);
        }
    }

}
