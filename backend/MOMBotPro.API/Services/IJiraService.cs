using MOMBotPro.API.Models;

namespace MOMBotPro.API.Services;

public interface IJiraService
{
    void Configure(
        string  domain,
        string  email,
        string  apiToken,
        string  projectKey,
        string? accountId = null,
        Guid?   userId    = null);

    Task<JiraTicket> CreateTicket(
        string bugSummary,
        string clientName,
        Guid?  userId = null);
}
