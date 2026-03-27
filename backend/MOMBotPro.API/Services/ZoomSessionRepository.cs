using Microsoft.EntityFrameworkCore;
using MOMBotPro.API.Data;
using MOMBotPro.API.Models;
using MOMBotPro.API.Models.Entities;

namespace MOMBotPro.API.Services;

public class ZoomSessionRepository
{
    private readonly ApplicationDbContext _db;

    public ZoomSessionRepository(ApplicationDbContext db)
    {
        _db = db;
    }

    // ── Create ───────────────────────────────────────────────────────────
    public ZoomSession Create(string botId, string meetingUrl, string clientName)
    {
        var session = new ZoomSession
        {
            BotId         = botId,
            MeetingUrl    = meetingUrl,
            ClientName    = clientName,
            Status        = ZoomSessionStatus.Joining,
            StatusMessage = "Bot is joining the Zoom call..."
        };

        var entity = ToEntity(session);
        _db.ZoomSessions.Add(entity);
        _db.SaveChanges();

        return session;
    }

    // ── Get ──────────────────────────────────────────────────────────────
    public ZoomSession? Get(string id)
    {
        if (!Guid.TryParse(id, out var guid)) return null;
        var entity = _db.ZoomSessions.Find(guid);
        return entity == null ? null : ToModel(entity);
    }

    // ── GetAll ───────────────────────────────────────────────────────────
    public List<ZoomSession> GetAll() =>
        _db.ZoomSessions
           .AsNoTracking()
           .OrderByDescending(s => s.CreatedAt)
           .AsEnumerable()
           .Select(ToModel)
           .ToList();

    // ── UpdateAsync — saves all mutable fields back to DB ────────────────
    public async Task UpdateAsync(ZoomSession session)
    {
        if (!Guid.TryParse(session.Id, out var guid)) return;

        var entity = await _db.ZoomSessions.FindAsync(guid);
        if (entity == null) return;

        entity.UserId        = session.UserId;
        entity.Status        = session.Status.ToString();
        entity.StatusMessage = session.StatusMessage;
        entity.PipelineId    = session.PipelineId != null &&
                               Guid.TryParse(session.PipelineId, out var pipeGuid)
                               ? pipeGuid
                               : null;

        await _db.SaveChangesAsync();
    }

    // ── Mapping helpers ──────────────────────────────────────────────────
    private static ZoomSessionEntity ToEntity(ZoomSession m) => new()
    {
        Id            = Guid.TryParse(m.Id, out var g) ? g : Guid.NewGuid(),
        UserId        = m.UserId,
        BotId         = m.BotId,
        MeetingUrl    = m.MeetingUrl,
        ClientName    = m.ClientName,
        Status        = m.Status.ToString(),
        StatusMessage = m.StatusMessage,
        PipelineId    = m.PipelineId != null && Guid.TryParse(m.PipelineId, out var pg)
                        ? pg : null,
        CreatedAt     = m.CreatedAt
    };

    private static ZoomSession ToModel(ZoomSessionEntity e) => new()
    {
        Id            = e.Id.ToString(),
        UserId        = e.UserId,
        BotId         = e.BotId,
        MeetingUrl    = e.MeetingUrl,
        ClientName    = e.ClientName,
        Status        = Enum.TryParse<ZoomSessionStatus>(e.Status, out var s)
                        ? s : ZoomSessionStatus.Joining,
        StatusMessage = e.StatusMessage ?? "",
        PipelineId    = e.PipelineId?.ToString(),
        CreatedAt     = e.CreatedAt
    };
}
