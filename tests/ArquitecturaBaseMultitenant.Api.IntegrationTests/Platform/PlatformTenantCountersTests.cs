using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

[Collection(ApiTestGroup.Name)]
public sealed class PlatformTenantCountersTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Forward_migration_preserves_existing_totals_without_recounting_private_rows()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var context = new ApplicationDbContext(options.Options, new TenantContext());
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("20261003191532_AddTenantFeatures", Ct);
        var id = Guid.NewGuid();
        await using (var connection = new NpgsqlConnection(isolated.AdminConnectionString))
        {
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("""
                INSERT INTO platform."Tenants" ("Id", "Kind", "Status", "Name", "CompanyCount", "MemberCount")
                VALUES (@id, 'Business', 'Active', 'Metadatos anteriores', 7, 11);
                """, connection);
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync(Ct);
        }
        await migrator.MigrateAsync("20261003213034_SeparateTenantCounters", Ct);
        var counters = await context.TenantCounters.AsNoTracking().SingleAsync(row => row.TenantId == id, Ct);
        Assert.Equal(7, counters.CompanyCount);
        Assert.Equal(11, counters.MemberCount);
        Assert.Equal(TenantStatus.Active, (await context.Tenants.AsNoTracking().SingleAsync(row => row.Id == id, Ct)).Status);
    }

    [Fact]
    public async Task A_private_insert_and_a_versioned_state_change_commit_together_without_ignoring_stale_versions()
    {
        var tenant = Tenant.CreateBusiness("Contador y estado", false);
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var work = services.GetRequiredService<IUnitOfWork>();
        await work.ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantRepository>().Add(tenant);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        var original = (await services.GetRequiredService<IPlatformReader>().FindTenantAsync(tenant.Id, Ct))!.Version;
        var repository = services.GetRequiredService<IPlatformTenantRepository>();
        using (services.GetRequiredService<ITenantScope>().Enter(tenant.Id))
        {
            var changed = await work.ExecuteInTransactionAsync(async ct =>
            {
                var current = await repository.GetForUpdateAsync(tenant.Id, original, ct);
                Assert.NotNull(current);
                Assert.True(current.Activate().IsSuccess);
                repository.MarkUpdated(current);
                services.GetRequiredService<IMemberRepository>().Add(Member.Invite());
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(changed.IsSuccess);
            Assert.Equal(1, await services.GetRequiredService<ApplicationDbContext>().Members.CountAsync(Ct));
        }
        var stored = await services.GetRequiredService<IPlatformReader>().FindTenantAsync(tenant.Id, Ct);
        Assert.NotNull(stored);
        Assert.Equal(TenantStatus.Active, stored.Status);
        Assert.Equal(1, stored.MemberCount);
        Assert.NotEqual(original, stored.Version);
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => work.ExecuteInTransactionAsync(async ct =>
        {
            var current = await repository.GetForUpdateAsync(tenant.Id, original, ct);
            Assert.NotNull(current);
            Assert.True(current.Suspend().IsSuccess);
            repository.MarkUpdated(current);
            return Result.Success();
        }, CommitPolicy.OnSuccess, Ct));
        Assert.Equal(TenantStatus.Active, (await services.GetRequiredService<IPlatformReader>().FindTenantAsync(tenant.Id, Ct))!.Status);
    }
}
