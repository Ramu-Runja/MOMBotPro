using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Controllers;

[Authorize]
[ApiController]
[Route("api/zoom-settings")]
public class ZoomSettingsController : ControllerBase
{
    private readonly ApplicationDbContext          _db;
    private readonly ILogger<ZoomSettingsController> _logger;

    public ZoomSettingsController(
        ApplicationDbContext            db,
        ILogger<ZoomSettingsController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    // GET /api/zoom-settings
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var s = await _db.ZoomSettings
            .FirstOrDefaultAsync(z => z.UserId == userId.Value);

        if (s == null) return Ok(null);

        return Ok(MapToDto(s));
    }

    // POST /api/zoom-settings
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] ZoomSettingsRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        TimeOnly? scheduledTime = null;
        if (!string.IsNullOrEmpty(req.ScheduledTime) &&
            TimeOnly.TryParse(req.ScheduledTime, out var parsed))
            scheduledTime = parsed;

        var existing = await _db.ZoomSettings
            .FirstOrDefaultAsync(z => z.UserId == userId.Value);

        if (existing == null)
        {
            existing = new ZoomSettings
            {
                Id        = Guid.NewGuid(),
                UserId    = userId.Value,
                CreatedAt = DateTime.UtcNow
            };
            _db.ZoomSettings.Add(existing);
        }

        existing.ZoomLink      = req.ZoomLink?.Trim() ?? "";
        existing.IsRecurring   = req.IsRecurring;
        existing.ScheduledTime = scheduledTime;
        existing.ScheduledDays = req.ScheduledDays?.Trim();
        existing.IsActive      = req.IsActive;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Zoom settings saved for userId={UserId}", userId);

        return Ok(MapToDto(existing));
    }

    // DELETE /api/zoom-settings
    [HttpDelete]
    public async Task<IActionResult> Delete()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var existing = await _db.ZoomSettings
            .FirstOrDefaultAsync(z => z.UserId == userId.Value);

        if (existing != null)
        {
            _db.ZoomSettings.Remove(existing);
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Zoom settings deleted." });
    }

    // ── Helpers ──────────────────────────────────────────────

    private static object MapToDto(ZoomSettings s) => new
    {
        id            = s.Id,
        zoomLink      = s.ZoomLink,
        isRecurring   = s.IsRecurring,
        scheduledTime = s.ScheduledTime?.ToString("HH:mm"),
        scheduledDays = s.ScheduledDays,
        isActive      = s.IsActive,
        createdAt     = s.CreatedAt
    };

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public class ZoomSettingsRequest
{
    public string? ZoomLink      { get; set; }
    public bool    IsRecurring   { get; set; }
    public string? ScheduledTime { get; set; }  // "HH:mm"
    public string? ScheduledDays { get; set; }  // "Mon,Tue,Wed,Thu,Fri"
    public bool    IsActive      { get; set; } = true;
}
