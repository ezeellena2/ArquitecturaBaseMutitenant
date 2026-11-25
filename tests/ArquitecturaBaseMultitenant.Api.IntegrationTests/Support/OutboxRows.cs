using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>
/// Deja una fila de la cola como la dejaría el despacho, que en Testing no corre solo: enviada, «Sin confirmar», fallida,
/// cancelada por vencida o purgada. Sirve para probar lo que se deriva de la cola al leer (B6).
/// </summary>
internal static class OutboxRows
{
    internal static async Task SetStatusAsync(IServiceProvider services, Guid id, OutboxStatus status, CancellationToken ct)
    {
        string? code = status switch
        {
            OutboxStatus.Uncertain => OutboxMessage.InterruptedCode,
            OutboxStatus.Cancelled => OutboxMessage.ExpiredCode,
            OutboxStatus.Failed => "Outbox.Transport.Failed",
            _ => null,
        };
        await using var scope = services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages
            .Where(message => message.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(message => message.Status, status)
                .SetProperty(message => message.LastErrorCode, code), ct));
    }

    /// <summary>La aplicación no borra filas de la cola salvo con la baja de una cuenta: se borra como dueño de la base.</summary>
    internal static async Task DeleteAsync(ApiFactory factory, Guid id, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("DELETE FROM platform.\"OutboxMessages\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", id);
        Assert.Equal(1, await command.ExecuteNonQueryAsync(ct));
    }
}
