using Microsoft.Data.SqlClient;
using System.Data;

namespace MOMBotPro.API.Data;

public class StoredProcedureHelper
{
    private readonly string _connectionString;

    public StoredProcedureHelper(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");
    }

    private SqlConnection Open() => new(_connectionString);

    // ── ExecuteReader: returns list of row-dictionaries ────
    public async Task<List<Dictionary<string, object?>>> ExecuteReaderAsync(
        string procedureName,
        Dictionary<string, object?>? parameters = null)
    {
        var results = new List<Dictionary<string, object?>>();

        await using var conn = Open();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(procedureName, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        AddParams(cmd, parameters);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            results.Add(row);
        }
        return results;
    }

    // ── ExecuteNonQuery ────────────────────────────────────
    public async Task<int> ExecuteNonQueryAsync(
        string procedureName,
        Dictionary<string, object?>? parameters = null)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(procedureName, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        AddParams(cmd, parameters);
        return await cmd.ExecuteNonQueryAsync();
    }

    // ── ExecuteScalar ──────────────────────────────────────
    public async Task<object?> ExecuteScalarAsync(
        string procedureName,
        Dictionary<string, object?>? parameters = null)
    {
        await using var conn = Open();
        await conn.OpenAsync();
        await using var cmd = new SqlCommand(procedureName, conn)
        {
            CommandType = CommandType.StoredProcedure
        };
        AddParams(cmd, parameters);
        return await cmd.ExecuteScalarAsync();
    }

    private static void AddParams(SqlCommand cmd, Dictionary<string, object?>? parameters)
    {
        if (parameters == null) return;
        foreach (var (key, value) in parameters)
            cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
    }
}
