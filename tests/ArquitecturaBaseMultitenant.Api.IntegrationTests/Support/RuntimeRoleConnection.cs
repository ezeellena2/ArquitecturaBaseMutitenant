using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>Abre SQL crudo con el rol runtime y el contexto de tenant esperado por RLS.</summary>
internal static class RuntimeRoleConnection
{
    public static async Task<NpgsqlConnection> OpenAsync(
        string connectionString, Guid? tenantId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("The tenant id cannot be empty.", nameof(tenantId));
        }

        var connection = new NpgsqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT current_user, set_config('app.tenant_id', @tenant_id, false)", connection);
            command.Parameters.AddWithValue("tenant_id", tenantId?.ToString("D") ?? string.Empty);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            if (!string.Equals(reader.GetString(0), "mt_app", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The connection does not use the runtime database role.");
            }

            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
