using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Las dos migraciones de las acciones (guía de migración, paso 4): el Down de <c>AssistantProposals</c> se niega con una
/// propuesta guardada y el de <c>AssistantTurnOfferedActions</c>, con un turno que ofreció acciones, antes de cualquier DDL y
/// con la RLS forzada; sin eso vuelven atrás, y las dos vuelven a subir. Las guardas corren como la migración, con el dueño de
/// las tablas y sin organización.
/// </summary>
public sealed class AssistantActionsMigrationTests
{
    private const string BeforeTurns = "20261126150701_TenantFeaturesBySource";
    private const string Turns = "20261126183300_AssistantTurnOfferedActions";
    private const string Proposals = "20261126184548_AssistantProposals";

    [Fact]
    public async Task Down_refuses_to_lose_a_proposal_or_the_offered_actions_of_a_turn_and_goes_back_and_up_without_them()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = AssistantTestData.Client(isolated);
        var owner = await AccountJourney.RegisterAsync(isolated, client, ct);
        var tenantId = await OrganizationJourney.StageAsync(isolated, owner, "Migración de acciones " + Guid.NewGuid().ToString("N")[..8], ct);
        var conversation = await AssistantTestData.ConversationAsync(isolated, tenantId, owner.UserId, Access.Business);
        var proposal = await AssistantTestData.ProposalAsync(isolated, tenantId, conversation.Id, owner.UserId, "roles.create", 1,
            new AssistantProposalValues(null, [AssistantTestData.Written("name", "Ventas")]));
        var turnId = await TurnWithOfferedActionsAsync(isolated, tenantId, conversation.Id, ct);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();

        // Con una propuesta guardada, el Down de las propuestas se niega: la tabla y la propuesta quedan, con la RLS forzada.
        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(Turns, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.Contains(Proposals, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.Equal(1L, await InTenantAsync("""SELECT count(*) FROM tenant."AssistantProposals" WHERE "Id" = @id""", proposal.Id));
        Assert.True(await RlsForcedAsync("AssistantProposals"));

        Assert.Equal(1L, await InTenantAsync("""WITH gone AS (DELETE FROM tenant."AssistantProposals" WHERE "Id" = @id RETURNING 1) SELECT count(*) FROM gone""", proposal.Id));
        await migrator.MigrateAsync(Turns, ct);
        Assert.DoesNotContain(Proposals, await db.Database.GetAppliedMigrationsAsync(ct));

        // Con un turno que ofreció acciones, el Down de la lista sellada se niega y la deja.
        refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(BeforeTurns, ct));
        Assert.Equal(PostgresErrorCodes.RaiseException, refused.SqlState);
        Assert.Contains(Turns, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.Equal(1L, await InTenantAsync("""SELECT count(*) FROM tenant."AssistantTurns" WHERE "Id" = @id AND "OfferedActions" IS NOT NULL""", turnId));
        Assert.True(await RlsForcedAsync("AssistantTurns"));

        Assert.Equal(1L, await InTenantAsync("""WITH sealed AS (UPDATE tenant."AssistantTurns" SET "OfferedActions" = NULL WHERE "Id" = @id RETURNING 1) SELECT count(*) FROM sealed""", turnId));
        await migrator.MigrateAsync(BeforeTurns, ct);
        Assert.DoesNotContain(Turns, await db.Database.GetAppliedMigrationsAsync(ct));

        await migrator.MigrateAsync(Proposals, ct);
        Assert.Contains(Proposals, await db.Database.GetAppliedMigrationsAsync(ct));
        Assert.True(await RlsForcedAsync("AssistantProposals"));
        Assert.False(db.Database.HasPendingModelChanges());

        async Task<bool> RlsForcedAsync(string table)
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand($"SELECT relforcerowsecurity FROM pg_class WHERE oid = 'tenant.\"{table}\"'::regclass", connection);
            return (bool)(await command.ExecuteScalarAsync(ct))!;
        }

        // El dueño también pasa por la RLS forzada: para ver o tocar una fila entra a su organización, como la app.
        async Task<long> InTenantAsync(string sql, Guid id)
        {
            await using var connection = new NpgsqlConnection(isolated.AdminConnectionString);
            await connection.OpenAsync(ct);
            await using (var enter = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tenant, false)", connection))
            {
                enter.Parameters.AddWithValue("tenant", tenantId.ToString());
                await enter.ExecuteNonQueryAsync(ct);
            }
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", id);
            return (long)(await command.ExecuteScalarAsync(ct))!;
        }
    }

    // Un turno de empresa que selló las acciones que ofreció (0068 §6).
    private static async Task<Guid> TurnWithOfferedActionsAsync(ApiFactory factory, Guid tenantId, Guid conversationId, CancellationToken ct)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var turn = AssistantTurn.Create(conversationId, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        turn.OfferActions(["users.invite@1"]);
        turn.Finish(nameof(AssistantTurnStatus.Complete));
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(turn);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, ct);
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
        return turn.Id;
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
