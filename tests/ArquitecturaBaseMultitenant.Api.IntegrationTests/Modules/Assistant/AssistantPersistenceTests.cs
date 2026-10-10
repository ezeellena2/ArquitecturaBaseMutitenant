using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantPersistenceTests(ApiFactory factory)
{
    [Fact]
    public async Task Incremental_title_limit_round_trips_eighty_characters_in_both_storage_sides()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        var tenantId = await AssistantTestData.ActiveTenantAsync(client);
        var conversation = await AssistantTestData.ConversationAsync(factory, tenantId, account.UserId, Access.Consumer);
        var title = new string('t', 80);
        var platform = PlatformAssistantConversation.Create(account.UserId, factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var db = services.GetRequiredService<ApplicationDbContext>();
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var row = await db.Set<AssistantConversation>().SingleAsync(value => value.Id == conversation.Id, ct);
                db.Entry(row).Property(value => value.Title).CurrentValue = title;
                db.Add(platform);
                db.Entry(platform).Property(value => value.Title).CurrentValue = title;
                return Result.Success();
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
        }
        using var reread = factory.Services.CreateScope();
        using var active = reread.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
        var context = reread.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(title, (await context.Set<AssistantConversation>().AsNoTracking().SingleAsync(value => value.Id == conversation.Id, AssistantTestData.Ct)).Title);
        Assert.Equal(title, (await context.Set<PlatformAssistantConversation>().AsNoTracking().SingleAsync(value => value.Id == platform.Id, AssistantTestData.Ct)).Title);
    }
    [Fact]
    public async Task Forward_upgrade_preserves_existing_reference_rows_and_installs_runtime_permissions_and_RLS()
    {
        var database = "assistant_upgrade_" + Guid.CreateVersion7().ToString("N");
        await using var bootstrap = new NpgsqlConnection(factory.BootstrapConnectionString);
        await bootstrap.OpenAsync(AssistantTestData.Ct);
        await using (var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\" OWNER mt_owner LOCALE_PROVIDER icu ICU_LOCALE 'es-AR' TEMPLATE template0", bootstrap))
            await create.ExecuteNonQueryAsync(AssistantTestData.Ct);
        var adminString = new NpgsqlConnectionStringBuilder(factory.AdminConnectionString) { Database = database }.ConnectionString;
        var runtimeString = new NpgsqlConnectionStringBuilder(factory.RuntimeConnectionString) { Database = database }.ConnectionString;
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(adminString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261008224533_LoginLinks", AssistantTestData.Ct);
        Assert.True(await DatabaseBootstrapExtensions.SeedReferenceDataAsync(adminString, AssistantTestData.Ct));
        var currency = await db.Set<Currency>().AsNoTracking().SingleAsync(value => value.Code == "ARS", AssistantTestData.Ct);
        var currencyCount = await db.Set<Currency>().CountAsync(AssistantTestData.Ct);
        var before = (await db.Database.GetAppliedMigrationsAsync(AssistantTestData.Ct)).ToArray();
        Assert.Equal("20261008224533_LoginLinks", before[^1]);
        await migrator.MigrateAsync("20261010011328_AssistantModule", AssistantTestData.Ct);
        var after = (await db.Database.GetAppliedMigrationsAsync(AssistantTestData.Ct)).ToArray();
        Assert.Equal(before.Length + 1, after.Length);
        Assert.Equal("20261010011328_AssistantModule", after[^1]);
        var preserved = await db.Set<Currency>().AsNoTracking().SingleAsync(value => value.Code == "ARS", AssistantTestData.Ct);
        Assert.Equal(currency.Symbol, preserved.Symbol);
        Assert.Equal(currency.MinorUnits, preserved.MinorUnits);
        Assert.Equal(currencyCount, await db.Set<Currency>().CountAsync(AssistantTestData.Ct));
        await using var runtime = new NpgsqlConnection(runtimeString);
        await runtime.OpenAsync(AssistantTestData.Ct);
        const string grants = """
            SELECT count(*) = 15,
                   bool_and(has_table_privilege(current_user, format('%I.%I', schemaname, tablename), 'SELECT')
                       AND has_table_privilege(current_user, format('%I.%I', schemaname, tablename), 'INSERT')
                       AND has_table_privilege(current_user, format('%I.%I', schemaname, tablename), 'UPDATE')
                       AND has_table_privilege(current_user, format('%I.%I', schemaname, tablename), 'DELETE')),
                   current_user = 'mt_app'
            FROM pg_tables
            WHERE schemaname IN ('tenant', 'platform') AND (tablename LIKE 'Assistant%' OR tablename LIKE 'PlatformAssistant%')
            """;
        await using (var inventory = new NpgsqlCommand(grants, runtime))
        await using (var reader = await inventory.ExecuteReaderAsync(AssistantTestData.Ct))
        {
            Assert.True(await reader.ReadAsync(AssistantTestData.Ct));
            Assert.True(reader.GetBoolean(0)); Assert.True(reader.GetBoolean(1)); Assert.True(reader.GetBoolean(2));
        }
        await using (var forced = new NpgsqlCommand("SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='tenant' AND c.relname LIKE 'Assistant%' AND c.relrowsecurity AND c.relforcerowsecurity", runtime))
            Assert.Equal(6L, await forced.ExecuteScalarAsync(AssistantTestData.Ct));
        var conversation = PlatformAssistantConversation.Create(Guid.CreateVersion7(), factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
        await using (var insert = new NpgsqlCommand("""
            INSERT INTO platform."PlatformAssistantConversations" ("Id", "OwnerUserId", "IsSaved", "CreatedAtUtc", "LastActivityAtUtc") VALUES (@id, @owner, false, @now, @now);
            SELECT count(*) FROM platform."PlatformAssistantConversations" WHERE "Id"=@id AND "OwnerUserId"=@owner
            """, runtime))
        {
            insert.Parameters.AddWithValue("id", conversation.Id); insert.Parameters.AddWithValue("owner", conversation.OwnerUserId);
            insert.Parameters.AddWithValue("now", conversation.CreatedAtUtc);
            Assert.Equal(1L, await insert.ExecuteScalarAsync(AssistantTestData.Ct));
        }
        await using var admin = new NpgsqlConnection(adminString);
        await admin.OpenAsync(AssistantTestData.Ct);
        await using (var revoke = new NpgsqlCommand("REVOKE UPDATE ON TABLE platform.\"PlatformAssistantConversations\" FROM mt_app", admin))
            await revoke.ExecuteNonQueryAsync(AssistantTestData.Ct);
        await using (var negative = new NpgsqlCommand(grants, runtime))
        await using (var reader = await negative.ExecuteReaderAsync(AssistantTestData.Ct))
        {
            Assert.True(await reader.ReadAsync(AssistantTestData.Ct));
            Assert.True(reader.GetBoolean(0)); Assert.False(reader.GetBoolean(1));
        }
    }
    [Fact]
    public async Task Global_settings_require_security_evidence_without_an_active_tenant()
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<ApplicationDbContext>();
        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var row = await db.Set<AssistantPlatformSettings>().SingleOrDefaultAsync(ct);
                if (row is null) db.Add(AssistantPlatformSettings.Create(true, new Money(0m, CurrencyCode.Create(AssistantContractPolicy.BillingCurrency).Value)));
                else db.Entry(row).Property(value => value.PersonalEnabled).CurrentValue = !row.PersonalEnabled;
                return Result.Success();
            }, CommitPolicy.OnSuccess, AssistantTestData.Ct));
        Assert.Contains("matching security event", rejected.Message, StringComparison.Ordinal);
        await AssistantTestData.PersonalAsync(factory, false);
    }

    [Fact]
    public async Task Catalog_provider_and_model_id_are_unique_and_prices_survive_runtime_round_trip()
    {
        var modelId = Guid.CreateVersion7().ToString("N");
        var price = new Money(1.123456m, CurrencyCode.Create(AssistantContractPolicy.BillingCurrency).Value);
        var first = AssistantModelPrice.Create("Anthropic", modelId, "Model fixture", price, price);
        var second = AssistantModelPrice.Create("Anthropic", modelId, "Same model fixture", price, price);
        await SaveModelAsync(first);
        var error = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => SaveModelAsync(second));
        Assert.Equal(UniqueKeys.AssistantModel, error.ConstraintName);
        using var scope = factory.Services.CreateScope();
        var read = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantModelPrice>().AsNoTracking()
            .SingleAsync(value => value.Id == first.Id, AssistantTestData.Ct);
        Assert.Equal(price, read.InputPrice);
    }

    private async Task SaveModelAsync(AssistantModelPrice model)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(model);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(AuditActorKind.System, Guid.Empty,
                "Assistant catalog fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
    }
    [Fact]
    public void Runtime_design_bootstrap_and_core_models_keep_the_explicit_contributions()
    {
        using var scope = factory.Services.CreateScope();
        using var design = new DesignTimeApplicationDbContextFactory().CreateDbContext([]);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql("Host=localhost;Database=model");
        options.UseOpenIddict<Guid>();
        using var bootstrap = new ApplicationDbContext(options.Options, new TenantContext());
        var runtime = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        PersistenceModelComparison.AssertEqual(runtime, design);
        PersistenceModelComparison.AssertEqual(runtime, bootstrap);
        using var core = new ApplicationDbContext(options.Options, new TenantContext(), []);
        Assert.Null(core.Model.FindEntityType(typeof(AssistantConversation)));
        Assert.Null(core.Model.FindEntityType(typeof(PlatformAssistantConversation)));
        Assert.Null(core.Model.FindEntityType(typeof(AssistantModelPrice)));
        Assert.Equal("xmin", runtime.Model.FindEntityType(typeof(AssistantConversation))!.FindProperty("Version")!.GetColumnName());
    }

    [Fact]
    public async Task Runtime_rows_survive_restart_are_tenant_stamped_and_all_private_tables_force_RLS()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        var tenantId = await AssistantTestData.ActiveTenantAsync(client);
        var conversation = await AssistantTestData.ConversationAsync(factory, tenantId, account.UserId, Access.Consumer);
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
            var context = services.GetRequiredService<ApplicationDbContext>();
            var message = AssistantMessage.Create(conversation.Id, "User", "Complete", "private-sentinel", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => { context.Add(message); return Task.FromResult(Result.Success()); }, CommitPolicy.OnSuccess, AssistantTestData.Ct);
            Assert.Equal(tenantId, message.TenantId);
        }
        using (var restart = factory.Services.CreateScope())
        {
            using var tenant = restart.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenantId);
            var context = restart.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal("private-sentinel", (await context.Set<AssistantMessage>().SingleAsync(value => value.ConversationId == conversation.Id, AssistantTestData.Ct)).Text);
        }
        await using var runtime = new NpgsqlConnection(factory.RuntimeConnectionString);
        await runtime.OpenAsync(AssistantTestData.Ct);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM tenant.\"AssistantConversations\" WHERE \"Id\" = @id", runtime);
        command.Parameters.AddWithValue("id", conversation.Id);
        Assert.Equal(0L, await command.ExecuteScalarAsync(AssistantTestData.Ct));
        await using var admin = new NpgsqlConnection(factory.AdminConnectionString);
        await admin.OpenAsync(AssistantTestData.Ct);
        await using var policies = new NpgsqlCommand("SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace WHERE n.nspname='tenant' AND c.relname LIKE 'Assistant%' AND c.relrowsecurity AND c.relforcerowsecurity", admin);
        Assert.Equal(6L, await policies.ExecuteScalarAsync(AssistantTestData.Ct));
    }
}
