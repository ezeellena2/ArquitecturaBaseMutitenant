using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// Desde la decisión 3 de la E11 el número de Plataforma vive en la base, y la colección ApiTestGroup la comparte: una
/// prueba que lo conecta desde la configuración lo deja conectado para las siguientes. Esto lo desconecta en la base y
/// hace que el host de la fábrica lo relea. Las pruebas que dicen «sin canal» lo llaman antes; las que lo conectan, antes
/// y después. Vive con el módulo: sin él no hay número que desconectar.
/// </summary>
internal static class PlatformWhatsAppNumberReset
{
    internal static async Task DisconnectAsync(ApiFactory factory, CancellationToken ct)
    {
        await using (var connection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                UPDATE identity."WhatsAppChannels" SET "Status" = 'Disconnected', "DisconnectedAtUtc" = now(), "EncryptedToken" = ''::bytea,
                    "EncryptedPin" = NULL
                WHERE "OrganizationId" IS NULL AND "Status" <> 'Disconnected'
                """, connection);
            await command.ExecuteNonQueryAsync(ct);
        }
        // El host compartido de la fábrica guarda el número en memoria; uno derivado lo lee de la base la primera vez.
        await factory.Services.GetRequiredService<IWhatsAppChannelCache>().RefreshAsync(ct);
    }
}
