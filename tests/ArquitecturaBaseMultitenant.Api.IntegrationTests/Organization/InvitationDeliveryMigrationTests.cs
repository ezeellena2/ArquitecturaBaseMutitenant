using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

/// <summary>
/// La migración <c>InvitationDeliveryOutbox</c> (B6) va y vuelve: el Up suma el mensaje de la cola de cada invitación y el
/// de la invitación de un operador; el Down vuelve atrás si nada se pierde (lo enviado se lee enviado igual) y se niega,
/// antes de cualquier DDL, si una invitación pendiente de la organización o de un operador tiene un mensaje que no salió:
/// sin el id, al volver a subir se leería «Enviada» para siempre y nadie la reenviaría (guía de migración, paso 4). La
/// guarda corre como la migración, con el dueño y sin organización, y la RLS de las invitaciones queda forzada.
/// </summary>
public sealed class InvitationDeliveryMigrationTests
{
    private const string Previous = "20261124235254_OutboxUncertainAndOrdering";
    private const string Current = "20261125004346_InvitationDeliveryOutbox";
    private static readonly string[] DeliveryColumns =
        ["AspNetUsers.OperatorInvitationOutboxMessageId YES", "Invitations.OutboxMessageId YES"];

    [Fact]
    public async Task Down_goes_back_when_nothing_is_lost_and_refuses_to_lose_a_pending_invitation_that_did_not_go_out()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false };
        using var client = isolated.CreateClient(options);
        var owner = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Entrega " + Guid.NewGuid().ToString("N")[..8], ct);
        await AccountJourney.AuthorizeAsync(client, "business", ct, tenantId);
        using var manager = isolated.CreateClient(options);
        var operatorManager = await AccountJourney.RegisterAsync(isolated, manager, ct);
        await PlatformOperators.GrantAsync(isolated.Services, operatorManager.UserId, PlatformOperators.All, ct);
        await AccountJourney.AuthorizeAsync(manager, "platform", ct);

        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        dbOptions.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(dbOptions.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();

        // Una invitación de la organización y una de operador que salieron: volver atrás no pierde nada.
        var (sentMember, sentOutbox) = await InviteMemberAsync();
        var (operatorId, operatorOutbox) = await InviteOperatorAsync();
        await OutboxRows.SetStatusAsync(isolated.Services, sentOutbox, OutboxStatus.Sent, ct);
        await OutboxRows.SetStatusAsync(isolated.Services, operatorOutbox, OutboxStatus.Sent, ct);
        await migrator.MigrateAsync(Previous, ct);
        Assert.Empty(await DeliveryColumnsAsync());
        Assert.True(await RlsForcedAsync());
        await migrator.MigrateAsync(Current, ct);
        Assert.Equal(DeliveryColumns, await DeliveryColumnsAsync());
        // La migración se verifica con su esquema; la Api exige después el esquema actual completo (las invitaciones leen
        // el nombre y los correos de la configuración, de una migración posterior).
        await migrator.MigrateAsync(null, ct);
        Assert.Equal("Sent", await MemberDeliveryAsync(sentMember));
        Assert.Equal("Sent", await OperatorDeliveryAsync(operatorId));

        // La de la organización agotó sus intentos: Usuarios lo dice, y el Down se niega antes que borrarlo.
        var (failedMember, failedOutbox) = await InviteMemberAsync();
        await OutboxRows.SetStatusAsync(isolated.Services, failedOutbox, OutboxStatus.Failed, ct);
        Assert.Equal("Failed", await MemberDeliveryAsync(failedMember));
        await AssertDownRefusesAsync();
        Assert.Equal("Failed", await MemberDeliveryAsync(failedMember));

        // Salió al final; ahora la del operador, reenviada, quedó «Sin confirmar»: también se niega.
        await OutboxRows.SetStatusAsync(isolated.Services, failedOutbox, OutboxStatus.Sent, ct);
        using (var resent = await PlatformJourney.PostAsync(manager, $"/api/platform/operators/{operatorId:D}/invitation/resend", new { }, ct))
            Assert.True(resent.StatusCode == HttpStatusCode.OK, await resent.Content.ReadAsStringAsync(ct));
        var resentOutbox = Assert.IsType<Guid>(await OperatorOutboxAsync(operatorId));
        await OutboxRows.SetStatusAsync(isolated.Services, resentOutbox, OutboxStatus.Uncertain, ct);
        Assert.Equal("Uncertain", await OperatorDeliveryAsync(operatorId));
        await AssertDownRefusesAsync();
        Assert.Equal("Uncertain", await OperatorDeliveryAsync(operatorId));
        Assert.False(db.Database.HasPendingModelChanges());

        async Task AssertDownRefusesAsync()
        {
            // Las posteriores vuelven primero, así la negativa es la de esta migración; después, otra vez el esquema actual.
            await migrator.MigrateAsync(Current, ct);
            var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(Previous, ct));
            Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
            Assert.Equal(DeliveryColumns, await DeliveryColumnsAsync());
            Assert.True(await RlsForcedAsync());
            Assert.Contains(Current, await db.Database.GetAppliedMigrationsAsync(ct));
            await migrator.MigrateAsync(null, ct);
        }

        async Task<(Guid MemberId, Guid OutboxId)> InviteMemberAsync()
        {
            using var issued = await AccountJourney.PostAsync(client, "/api/users/invitations", new
            {
                email = $"entrega-migracion-{Guid.NewGuid():N}@example.test",
                roleIds = Array.Empty<Guid>(),
                companies = Array.Empty<object>(),
            }, ct);
            Assert.True(issued.StatusCode == HttpStatusCode.Created, await issued.Content.ReadAsStringAsync(ct));
            using var json = JsonDocument.Parse(await issued.Content.ReadAsStringAsync(ct));
            var invitationId = json.RootElement.GetProperty("invitationId").GetGuid();
            await using var scope = isolated.Services.CreateAsyncScope();
            using var tenant = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var invitation = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Invitations.AsNoTracking()
                .SingleAsync(value => value.Id == invitationId, ct);
            return (invitation.MemberId, Assert.IsType<Guid>(invitation.OutboxMessageId));
        }

        async Task<(Guid AccountId, Guid OutboxId)> InviteOperatorAsync()
        {
            using var invited = await PlatformJourney.PostAsync(manager, "/api/platform/operators/invitations", new
            {
                email = $"operador-migracion-{Guid.NewGuid():N}@example.test",
                permissions = new[] { PlatformPermissions.Tenants.Read },
            }, ct);
            Assert.True(invited.StatusCode == HttpStatusCode.Created, await invited.Content.ReadAsStringAsync(ct));
            using var json = JsonDocument.Parse(await invited.Content.ReadAsStringAsync(ct));
            var id = json.RootElement.GetProperty("id").GetGuid();
            return (id, Assert.IsType<Guid>(await OperatorOutboxAsync(id)));
        }

        async Task<Guid?> OperatorOutboxAsync(Guid accountId)
        {
            await using var scope = isolated.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.AsNoTracking()
                .Where(user => user.Id == accountId).Select(user => user.OperatorInvitationOutboxMessageId).SingleAsync(ct);
        }

        async Task<string?> MemberDeliveryAsync(Guid memberId)
        {
            using var response = await client.GetAsync($"/api/users/{memberId:D}", ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
            using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return user.RootElement.GetProperty("invitationDelivery").GetString();
        }

        async Task<string?> OperatorDeliveryAsync(Guid accountId)
        {
            using var response = await manager.GetAsync($"/api/platform/accounts/{accountId:D}", ct);
            Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(ct));
            using var account = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return account.RootElement.GetProperty("invitationDelivery").GetString();
        }

        async Task<string[]> DeliveryColumnsAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("""
                SELECT table_name || '.' || column_name || ' ' || is_nullable FROM information_schema.columns
                WHERE (table_schema = 'tenant' AND table_name = 'Invitations' AND column_name = 'OutboxMessageId')
                    OR (table_schema = 'identity' AND table_name = 'AspNetUsers' AND column_name = 'OperatorInvitationOutboxMessageId')
                ORDER BY 1
                """, connection);
            var columns = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(0));
            return [.. columns];
        }

        async Task<bool> RlsForcedAsync()
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand(
                "SELECT relforcerowsecurity FROM pg_class WHERE oid = 'tenant.\"Invitations\"'::regclass", connection);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
