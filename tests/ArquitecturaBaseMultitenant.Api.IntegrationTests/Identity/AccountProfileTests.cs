using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// Comprueba la edición del perfil usando su versión leída. Impide que un guardado con versión vieja
/// sobrescriba preferencias ya actualizadas.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AccountProfileTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Profile_requires_its_read_version_and_stale_save_preserves_first_preferences()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(factory, client, Ct);
        using var read = await client.GetAsync("/api/me", Ct);
        using var before = await read.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        var version = before!.RootElement.GetProperty("version").GetUInt32();
        Assert.False(before.RootElement.GetProperty("needsPersonalLoginMethod").GetBoolean());
        using var missing = await client.PutAsJsonAsync("/api/me", new
            { firstName = "Persona", lastName = (string?)null, culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires" }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var first = await client.PutAsJsonAsync("/api/me", new
            { firstName = "Persona", lastName = (string?)null, culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires", version }, Ct);
        var firstVersion = await SavedVersion.ReadAsync(first, Ct);
        Assert.NotEqual(version, firstVersion);
        using var stale = await client.PutAsJsonAsync("/api/me", new
            { firstName = "Otra", lastName = "persona", culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires", version }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var problem = await stale.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("General.ConcurrencyConflict", problem!.RootElement.GetProperty("code").GetString());
        // Sin cambios también se compara la versión: si no, la respuesta daría una versión que la pantalla no vio.
        using var staleWithoutChanges = await client.PutAsJsonAsync("/api/me", new
            { firstName = "Persona", lastName = (string?)null, culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires", version }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, staleWithoutChanges.StatusCode);
        // El segundo guardado sale con la versión que respondió el primero, sin releer.
        using var second = await client.PutAsJsonAsync("/api/me", new
            { firstName = "Persona", lastName = (string?)null, culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires", version = firstVersion }, Ct);
        var secondVersion = await SavedVersion.ReadAsync(second, Ct);
        using var loaded = await client.GetAsync("/api/me", Ct);
        using var after = await loaded.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal("en-US", after!.RootElement.GetProperty("culture").GetString());
        Assert.Equal("Persona", after.RootElement.GetProperty("displayName").GetString());
        Assert.Equal(secondVersion, after.RootElement.GetProperty("version").GetUInt32());
    }

    /// <summary>
    /// «Tus datos» tiene su propia versión: la fila de la cuenta también cambia al entrar a otra organización, al cambiar
    /// el contacto principal, al contar o reiniciar los intentos fallidos y al cerrar las sesiones, y nada de eso es del
    /// perfil. Guardar con la versión leída antes no da un 409 falso.
    /// </summary>
    [Fact]
    public async Task Other_writes_on_the_account_do_not_make_saving_the_profile_conflict()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, account, "Organización de Tus datos", Ct);
        var version = await ProfileVersionAsync(client);

        // Otra pestaña u otro dispositivo: entra a la organización y la cuenta la recuerda como la última.
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                await services.GetRequiredService<IUserRepository>()
                    .SetPrimaryContactAsync(account.UserId, account.Email, null, ct);
                var signIn = services.GetRequiredService<ISignInService>();
                await signIn.RegisterFailedAttemptAsync(account.UserId, ct);
                await signIn.ResetFailedAttemptsAsync(account.UserId, ct);
                await signIn.RevokeSessionsAsync(account.UserId, ct);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        // Con las sesiones cerradas, vuelve a ingresar y guarda «Tus datos» con la versión que había leído.
        await SignInAgainAsync(client, account.Email.Value);
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        using var saved = await client.PutAsJsonAsync("/api/me", new
            { firstName = "Persona", lastName = (string?)null, culture = "en-US", timeZoneId = "America/Argentina/Buenos_Aires", version }, Ct);
        var savedVersion = await SavedVersion.ReadAsync(saved, Ct);
        Assert.NotEqual(version, savedVersion);
        Assert.Equal(savedVersion, await ProfileVersionAsync(client));

        // Otro guardado del perfil con la versión vieja sí choca.
        using var stale = await client.PutAsJsonAsync("/api/me", new
            { firstName = "Otra", lastName = "persona", culture = "es-AR", timeZoneId = "America/Argentina/Buenos_Aires", version }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    private static async Task<uint> ProfileVersionAsync(HttpClient client)
    {
        using var me = await client.GetAsync("/api/me", Ct);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var json = await me.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        return json!.RootElement.GetProperty("version").GetUInt32();
    }

    /// <summary>Ingresa con código; el código del registro se corre una hora para que la espera entre pedidos no frene este.</summary>
    private async Task SignInAgainAsync(HttpClient client, string email)
    {
        await using (var connection = new NpgsqlConnection(factory.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""
                UPDATE identity."LoginCodes" SET "CreatedAtUtc" = "CreatedAtUtc" - interval '1 hour'
                WHERE "Destination" = @email
                """, connection);
            command.Parameters.AddWithValue("email", email);
            await command.ExecuteNonQueryAsync(Ct);
        }

        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email, Ct);
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email, code, returnUrl = "/connect/authorize?access=consumer" }, Ct);
        Assert.True(verified.StatusCode == HttpStatusCode.OK, await verified.Content.ReadAsStringAsync(Ct));
    }
}
