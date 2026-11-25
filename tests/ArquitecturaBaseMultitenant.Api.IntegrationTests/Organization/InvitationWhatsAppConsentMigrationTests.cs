using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// La migración del consentimiento de «Cargar teléfonos» (base de la 12, decisión 179): el Up suma quién lo confirmó y
/// cuándo; el Down vuelve atrás si ninguna invitación lo tiene y, si alguna lo tiene, se niega antes que borrar la prueba.
/// La guarda corre como la migración, con el dueño de la tabla y sin organización, y la RLS queda forzada.
/// </summary>
public sealed class InvitationWhatsAppConsentMigrationTests
{
    [Fact]
    public async Task Down_goes_back_without_a_confirmed_consent_and_refuses_to_lose_one()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = isolated.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Consentimiento " + Guid.NewGuid().ToString("N")[..8], ct);
        await AccountJourney.AuthorizeAsync(client, "business", ct, tenantId);
        // Una invitación por correo, que no lleva consentimiento.
        using var issued = await AccountJourney.PostAsync(client, "/api/users/invitations", new
        {
            email = $"consentimiento-{Guid.NewGuid():N}@example.test",
            roleIds = Array.Empty<Guid>(),
            companies = Array.Empty<object>(),
        }, ct);
        Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(ct));
        using var json = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(ct));
        var invitationId = json.RootElement.GetProperty("invitationId").GetGuid();
        // Su correo ya salió: con el mensaje en la cola, la vuelta atrás de la B6 (InvitationDeliveryOutbox) se negaría.
        var outboxId = await InTenantAsync("""SELECT "OutboxMessageId" FROM tenant."Invitations" WHERE "Id" = @invitation""",
            async command => (Guid)(await command.ExecuteScalarAsync(ct))!);
        await OutboxRows.SetStatusAsync(isolated.Services, outboxId, OutboxStatus.Sent, ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261109002020_RetireLoginLinks";
        const string current = "20261109003140_InvitationWhatsAppConsent";
        string[] consentColumns = ["ConsentConfirmedAtUtc YES", "ConsentConfirmedByUserId YES"];

        await migrator.MigrateAsync(previous, ct);
        Assert.Empty(await ConsentColumnsAsync());
        Assert.True(await RlsForcedAsync());
        Assert.Equal(1L, await InTenantAsync("""SELECT count(*) FROM tenant."Invitations" WHERE "Id" = @invitation""",
            async command => (long)(await command.ExecuteScalarAsync(ct))!));

        await migrator.MigrateAsync(current, ct);
        Assert.Equal(consentColumns, await ConsentColumnsAsync());
        Assert.Equal(1, await InTenantAsync("""
            UPDATE tenant."Invitations" SET "ConsentConfirmedByUserId" = @owner, "ConsentConfirmedAtUtc" = now()
            WHERE "Id" = @invitation
            """, command => command.ExecuteNonQueryAsync(ct)));

        // Sin entrar a la organización, el dueño no la ve: por eso la guarda apaga el FORCE mientras mira.
        Assert.Equal(0L, await VisibleWithoutTenantAsync());
        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(previous, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.Equal(consentColumns, await ConsentColumnsAsync());
        Assert.Equal(2, await InTenantAsync("""
            SELECT num_nonnulls("ConsentConfirmedByUserId", "ConsentConfirmedAtUtc") FROM tenant."Invitations" WHERE "Id" = @invitation
            """, async command => (int)(await command.ExecuteScalarAsync(ct))!));
        Assert.True(await RlsForcedAsync());
        Assert.Contains(current, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.False(db.Database.HasPendingModelChanges());

        async Task<string[]> ConsentColumnsAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT column_name || ' ' || is_nullable FROM information_schema.columns
                WHERE table_schema = 'tenant' AND table_name = 'Invitations' AND column_name LIKE 'Consent%'
                ORDER BY column_name
                """, connection);
            var columns = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(0));
            return [.. columns];
        }

        async Task<long> VisibleWithoutTenantAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM tenant.\"Invitations\" WHERE \"Id\" = @invitation", connection);
            command.Parameters.AddWithValue("invitation", invitationId);
            return (long)(await command.ExecuteScalarAsync(ct))!;
        }

        async Task<bool> RlsForcedAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(
                "SELECT relforcerowsecurity FROM pg_class WHERE oid = 'tenant.\"Invitations\"'::regclass", connection);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }

        // El dueño también pasa por la RLS forzada: para ver o tocar la invitación entra a su organización, como la app.
        async Task<T> InTenantAsync<T>(string sql, Func<NpgsqlCommand, Task<T>> run)
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using (var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", connection))
            {
                enter.Parameters.AddWithValue("tenant", tenantId.ToString());
                await enter.ExecuteNonQueryAsync(ct);
            }
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("invitation", invitationId);
            if (sql.Contains("@owner", StringComparison.Ordinal)) command.Parameters.AddWithValue("owner", owner.UserId);
            return await run(command);
        }
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
