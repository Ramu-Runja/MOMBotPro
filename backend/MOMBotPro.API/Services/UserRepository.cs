using MOMBotPro.API.Data;
using MOMBotPro.API.Models;

namespace MOMBotPro.API.Services;

/// <summary>
/// Handles user and integration CRUD via stored procedures.
/// Falls back to in-memory store when SQL Server is unavailable.
/// </summary>
public class UserRepository
{
    private readonly StoredProcedureHelper? _sp;
    private readonly bool _useDb;

    // In-memory fallback
    private static readonly List<AppUser>     _users        = new();
    private static readonly List<Integration> _integrations = new();

    public UserRepository(IConfiguration config)
    {
        var cs = config.GetConnectionString("DefaultConnection");
        _useDb = !string.IsNullOrWhiteSpace(cs) &&
                 !cs.Contains("YOUR_SERVER") &&
                 !cs.Contains("DISABLED");

        if (_useDb)
        {
            try { _sp = new StoredProcedureHelper(config); }
            catch { _useDb = false; }
        }
    }

    // ── Users ──────────────────────────────────────────────
    public async Task<AppUser?> GetByEmailAsync(string email)
    {
        if (!_useDb)
            return _users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

        var rows = await _sp!.ExecuteReaderAsync("sp_GetUserByEmail",
            new() { ["@Email"] = email });
        return rows.Count == 0 ? null : MapUser(rows[0]);
    }

    public async Task<AppUser?> GetByIdAsync(Guid id)
    {
        if (!_useDb)
            return _users.FirstOrDefault(u => u.Id == id);

        var rows = await _sp!.ExecuteReaderAsync("sp_GetUserById",
            new() { ["@Id"] = id });
        return rows.Count == 0 ? null : MapUser(rows[0]);
    }

    public async Task<AppUser> CreateAsync(AppUser user)
    {
        if (!_useDb)
        {
            _users.Add(user);
            return user;
        }

        var rows = await _sp!.ExecuteReaderAsync("sp_CreateUser", new()
        {
            ["@Id"]           = user.Id,
            ["@FullName"]     = user.FullName,
            ["@Email"]        = user.Email,
            ["@PasswordHash"] = user.PasswordHash,
            ["@CompanyName"]  = user.CompanyName,
            ["@Domain"]       = user.Domain,
        });
        return rows.Count > 0 ? MapUser(rows[0]) : user;
    }

    public async Task DecrementTrialAsync(Guid userId)
    {
        if (!_useDb)
        {
            var u = _users.FirstOrDefault(u => u.Id == userId);
            if (u != null && u.FreeTrialMeetingsLeft > 0) u.FreeTrialMeetingsLeft--;
            return;
        }
        await _sp!.ExecuteNonQueryAsync("sp_DecrementFreeTrial",
            new() { ["@UserId"] = userId });
    }

    public async Task UpgradePlanAsync(Guid userId, string plan, decimal priceUsd, decimal priceInr)
    {
        if (!_useDb)
        {
            var u = _users.FirstOrDefault(u => u.Id == userId);
            if (u != null) { u.SubscriptionPlan = plan; u.FreeTrialMeetingsLeft = 9999; }
            return;
        }
        await _sp!.ExecuteNonQueryAsync("sp_UpdateSubscription", new()
        {
            ["@UserId"]   = userId,
            ["@Plan"]     = plan,
            ["@PriceUSD"] = priceUsd,
            ["@PriceINR"] = priceInr,
        });
    }

    // ── Integrations ──────────────────────────────────────
    public async Task<List<Integration>> GetIntegrationsAsync(Guid userId)
    {
        if (!_useDb)
            return _integrations.Where(i => i.UserId == userId).ToList();

        var rows = await _sp!.ExecuteReaderAsync("sp_GetIntegrationsByUser",
            new() { ["@UserId"] = userId });
        return rows.Select(MapIntegration).ToList();
    }

    public async Task<Integration> SaveIntegrationAsync(Integration integration)
    {
        if (!_useDb)
        {
            var existing = _integrations.FirstOrDefault(i =>
                i.UserId == integration.UserId && i.Type == integration.Type);
            if (existing != null)
            {
                existing.ConfigJson  = integration.ConfigJson;
                existing.IsConnected = integration.IsConnected;
                existing.ConnectedAt = integration.ConnectedAt;
                return existing;
            }
            _integrations.Add(integration);
            return integration;
        }

        var rows = await _sp!.ExecuteReaderAsync("sp_SaveIntegration", new()
        {
            ["@UserId"]      = integration.UserId,
            ["@Type"]        = integration.Type,
            ["@ConfigJson"]  = integration.ConfigJson ?? "{}",
            ["@IsConnected"] = integration.IsConnected,
        });
        return rows.Count > 0 ? MapIntegration(rows[0]) : integration;
    }

    public async Task DeleteIntegrationAsync(Guid userId, string type)
    {
        if (!_useDb)
        {
            _integrations.RemoveAll(i => i.UserId == userId && i.Type == type);
            return;
        }
        await _sp!.ExecuteNonQueryAsync("sp_SaveIntegration", new()
        {
            ["@UserId"]      = userId,
            ["@Type"]        = type,
            ["@ConfigJson"]  = "{}",
            ["@IsConnected"] = false,
        });
    }

    // ── Mappers ───────────────────────────────────────────
    private static AppUser MapUser(Dictionary<string, object?> r) => new()
    {
        Id                    = ParseGuid(r, "Id"),
        FullName              = r.GetValueOrDefault("FullName")?.ToString()         ?? "",
        Email                 = r.GetValueOrDefault("Email")?.ToString()            ?? "",
        PasswordHash          = r.GetValueOrDefault("PasswordHash")?.ToString()     ?? "",
        CompanyName           = r.GetValueOrDefault("CompanyName")?.ToString()      ?? "",
        Domain                = r.GetValueOrDefault("Domain")?.ToString()           ?? "",
        Role                  = r.GetValueOrDefault("Role")?.ToString()             ?? "User",
        SubscriptionPlan      = r.GetValueOrDefault("SubscriptionPlan")?.ToString() ?? "free_trial",
        FreeTrialMeetingsLeft = r.TryGetValue("FreeTrialMeetingsLeft", out var fl) ? Convert.ToInt32(fl) : 3,
        IsActive              = r.TryGetValue("IsActive", out var ia) && Convert.ToBoolean(ia),
    };

    private static Integration MapIntegration(Dictionary<string, object?> r) => new()
    {
        Id          = ParseGuid(r, "Id"),
        UserId      = ParseGuid(r, "UserId"),
        Type        = r.GetValueOrDefault("Type")?.ToString()       ?? "",
        ConfigJson  = r.GetValueOrDefault("ConfigJson")?.ToString(),
        IsConnected = r.TryGetValue("IsConnected", out var ic) && Convert.ToBoolean(ic),
        ConnectedAt = r.TryGetValue("ConnectedAt", out var ca) && ca is DateTime dt ? dt : null,
    };

    private static Guid ParseGuid(Dictionary<string, object?> r, string key)
    {
        if (!r.TryGetValue(key, out var val)) return Guid.Empty;
        if (val is Guid g) return g;
        return Guid.TryParse(val?.ToString(), out var p) ? p : Guid.Empty;
    }
}
