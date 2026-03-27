using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;
using MOMBotPro.API.Models;
using MOMBotPro.API.Services;

namespace MOMBotPro.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserRepository _users;
    private readonly TokenService   _tokens;

    // Personal email domains blocked from registration
    private static readonly HashSet<string> BlockedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com","yahoo.com","hotmail.com","outlook.com","icloud.com",
        "rediffmail.com","ymail.com","live.com","aol.com","protonmail.com",
        "zoho.com","mail.com","inbox.com","me.com","msn.com","yahoo.co.in",
        "yahoo.co.uk","hotmail.co.uk","googlemail.com"
    };

    public AuthController(UserRepository users, TokenService tokens)
    {
        _users  = users;
        _tokens = tokens;
    }

    // ── POST /api/auth/register ────────────────────────────
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))       return BadRequest(new { error = "Email is required." });
        if (string.IsNullOrWhiteSpace(req.FullName))    return BadRequest(new { error = "Full name is required." });
        if (string.IsNullOrWhiteSpace(req.CompanyName)) return BadRequest(new { error = "Company name is required." });
        if (string.IsNullOrWhiteSpace(req.Password))    return BadRequest(new { error = "Password is required." });
        if (req.Password != req.ConfirmPassword)        return BadRequest(new { error = "Passwords do not match." });
        if (req.Password.Length < 8)                    return BadRequest(new { error = "Password must be at least 8 characters." });

        // Domain validation
        var domain = req.Email.Split('@').LastOrDefault()?.ToLower() ?? "";
        if (BlockedDomains.Contains(domain))
            return BadRequest(new { error = "Please use your company email address. Personal email domains are not allowed." });

        // Check duplicate
        var existing = await _users.GetByEmailAsync(req.Email);
        if (existing != null) return Conflict(new { error = "An account with this email already exists." });

        var user = new AppUser
        {
            FullName    = req.FullName.Trim(),
            Email       = req.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            CompanyName = req.CompanyName.Trim(),
            Domain      = domain,
        };

        var created = await _users.CreateAsync(user);
        var token   = _tokens.Generate(created);

        return Ok(new AuthResponse { Token = token, User = SanitizeUser(created) });
    }

    // ── POST /api/auth/login ───────────────────────────────
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Email and password are required." });

        var user = await _users.GetByEmailAsync(req.Email.Trim().ToLower());
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized(new { error = "Invalid email or password." });

        if (!user.IsActive)
            return Unauthorized(new { error = "Account is deactivated. Contact support." });

        var token = _tokens.Generate(user);
        return Ok(new AuthResponse { Token = token, User = SanitizeUser(user) });
    }

    // ── POST /api/auth/logout ──────────────────────────────
    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout() => Ok(new { message = "Logged out successfully." });

    // ── GET /api/auth/me ───────────────────────────────────
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (idClaim == null || !Guid.TryParse(idClaim, out var userId))
            return Unauthorized();

        var user = await _users.GetByIdAsync(userId);
        if (user == null) return NotFound();

        return Ok(SanitizeUser(user));
    }

    private static AppUser SanitizeUser(AppUser u) => new()
    {
        Id                    = u.Id,
        FullName              = u.FullName,
        Email                 = u.Email,
        CompanyName           = u.CompanyName,
        Domain                = u.Domain,
        Role                  = u.Role,
        SubscriptionPlan      = u.SubscriptionPlan,
        FreeTrialMeetingsLeft = u.FreeTrialMeetingsLeft,
        IsActive              = u.IsActive,
        CreatedAt             = u.CreatedAt,
        PasswordHash          = "" // never send hash
    };
}
