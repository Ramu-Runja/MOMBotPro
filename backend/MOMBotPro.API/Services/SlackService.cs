using System.Net.Http.Json;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Services;

public class SlackService
{
    private readonly IHttpClientFactory    _httpFactory;
    private readonly IConfiguration        _config;
    private readonly ILogger<SlackService> _logger;

    public SlackService(
        IHttpClientFactory    httpFactory,
        IConfiguration        config,
        ILogger<SlackService> logger)
    {
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;
    }

    /// <summary>
    /// Sends a Slack message via bot token (chat.postMessage) when available,
    /// falling back to the incoming webhook URL stored in Integration.Domain
    /// or the global Slack:WebhookUrl config value.
    /// </summary>
    public async Task SendAsync(Integration? slackInteg, string text, object blocks)
    {
        var botToken   = slackInteg?.AccessToken;
        var webhookUrl = slackInteg?.Domain ?? _config["Slack:WebhookUrl"];

        if (!string.IsNullOrEmpty(botToken))
        {
            // Bot-token path: POST to chat.postMessage; channel stored in Email column
            var channel = slackInteg?.Email ?? "#general";
            await SendViaBotTokenAsync(botToken, channel, text, blocks);
        }
        else if (!string.IsNullOrEmpty(webhookUrl))
        {
            await SendViaWebhookAsync(webhookUrl, text, blocks);
        }
        else
        {
            _logger.LogDebug("Slack not configured — notification skipped");
        }
    }

    private async Task SendViaBotTokenAsync(
        string botToken, string channel, string text, object blocks)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", botToken);

            var payload = new { channel, text, blocks };
            var res = await http.PostAsJsonAsync("https://slack.com/api/chat.postMessage", payload);
            _logger.LogInformation("Slack chat.postMessage → {Status}", (int)res.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Slack bot-token send failed: {Msg}", ex.Message);
        }
    }

    private async Task SendViaWebhookAsync(string webhookUrl, string text, object blocks)
    {
        try
        {
            var http    = _httpFactory.CreateClient();
            var payload = new { text, blocks };
            await http.PostAsJsonAsync(webhookUrl, payload);
            _logger.LogInformation("Slack webhook notification sent");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Slack webhook send failed: {Msg}", ex.Message);
        }
    }
}
