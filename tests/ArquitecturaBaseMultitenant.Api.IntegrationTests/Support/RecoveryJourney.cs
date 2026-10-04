using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>Registra un pedido de «Recuperar mi cuenta» por la API pública, como lo haría la pantalla.</summary>
internal static class RecoveryJourney
{
    internal static async Task SubmitAsync(ApiFactory factory, HttpClient client, string newEmail,
        string previousEmail, CancellationToken ct)
    {
        var nonce = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var requested = await AccountJourney.PostAsync(client, "/api/account-recovery/code",
            new { email = newEmail, nonce }, ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, newEmail, ct);
        using var verified = await AccountJourney.PostAsync(client, "/api/account-recovery/verify",
            new { email = newEmail, nonce, code }, ct);
        Assert.Equal(HttpStatusCode.NoContent, verified.StatusCode);
        using var submitted = await AccountJourney.PostAsync(client, "/api/account-recovery",
            new { email = newEmail, nonce, previousEmail }, ct);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
    }

    /// <summary>Corre hacia atrás los códigos de un destino para que los límites de reenvío no frenen el paso siguiente.</summary>
    internal static async Task AgeCodesAsync(ApiFactory factory, string destination, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(factory.AdminConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE identity."LoginCodes" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour'
            WHERE "Destination" = @destination
            """, connection);
        command.Parameters.AddWithValue("destination", destination);
        await command.ExecuteNonQueryAsync(ct);
    }
}
