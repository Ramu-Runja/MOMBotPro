using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MOMBotPro.API.Services;

namespace MOMBotPro.API.Controllers;

[ApiController]
[Route("api/subscription")]
public class SubscriptionController : ControllerBase
{
    private readonly UserRepository _users;

    private static readonly object[] Plans = new[]
    {
        new {
            id = "free_trial", name = "Free Trial", price = 0, currency = "USD",
            inrPrice = (int?)null, savings = (string?)null,
            meetings = (object)"3",
            features = new[] { "3 free meetings", "Full 6-step pipeline", "MOM generation", "GitHub PR creation", "Jira ticket creation" }
        },
        new {
            id = "monthly", name = "Monthly Plan", price = 12, currency = "USD",
            inrPrice = (int?)1000, savings = (string?)null,
            meetings = (object)"Unlimited",
            features = new[] { "Unlimited meetings", "All integrations", "Analytics dashboard", "Priority support", "Email notifications" }
        },
        new {
            id = "yearly", name = "Yearly Plan", price = 84, currency = "USD",
            inrPrice = (int?)7000, savings = (string?)"Save 42%",
            meetings = (object)"Unlimited",
            features = new[] { "Everything in Monthly", "Custom domain", "Team accounts (up to 10)", "Dedicated support", "Early access to features" }
        }
    };

    public SubscriptionController(UserRepository users) => _users = users;

    // ── GET /api/subscription/plans ───────────────────────
    [HttpGet("plans")]
    public IActionResult GetPlans() => Ok(Plans);

    // ── GET /api/subscription/status ──────────────────────
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatus()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var user = await _users.GetByIdAsync(userId.Value);
        if (user == null) return NotFound();

        return Ok(new
        {
            plan                  = user.SubscriptionPlan,
            freeTrialMeetingsLeft = user.FreeTrialMeetingsLeft,
            isTrialExpired        = user.SubscriptionPlan == "free_trial" && user.FreeTrialMeetingsLeft <= 0,
        });
    }

    // ── POST /api/subscription/upgrade ───────────────────
    [HttpPost("upgrade")]
    [Authorize]
    public async Task<IActionResult> Upgrade([FromBody] MOMBotPro.API.Models.UpgradeRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var (priceUsd, priceInr) = req.PlanId switch
        {
            "monthly" => (12m, 1000m),
            "yearly"  => (84m, 7000m),
            _         => (0m, 0m)
        };

        await _users.UpgradePlanAsync(userId.Value, req.PlanId, priceUsd, priceInr);
        return Ok(new { message = $"Upgraded to {req.PlanId} successfully.", plan = req.PlanId });
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
