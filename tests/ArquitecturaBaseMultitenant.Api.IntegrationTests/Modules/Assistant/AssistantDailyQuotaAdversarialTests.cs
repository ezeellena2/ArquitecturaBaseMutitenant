using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Revisión adversarial de la B14 (271): una reserva de empresa hecha antes de la migración vive en la fila del cupo sin
/// organización. Esta versión la cierra en la fila de su organización: sin que el Up la mueva ahí, lanzaba «The reservation
/// lost its quota.», el turno no terminaba nunca y el cupo reservado no volvía. Lo mismo un paso ya consumido, cuyo resumen
/// lee la fila de su organización. Va con una base propia, porque baja y sube la migración.
/// </summary>
public sealed class AssistantDailyQuotaAdversarialTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);
    private static AssistantPriceSnapshot Price() => AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "fixture-haiku", "price-v1",
        Usd(.1m), Usd(.5m), Usd(.125m), Usd(.2m), Usd(.01m), null, null, null, null, null, null);
    private static FakeTimeProvider Clock(DateTime nowUtc) => new(new DateTimeOffset(nowUtc));

    [Fact]
    public async Task A_business_reservation_made_before_the_migration_still_returns_its_quota()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = AssistantTestData.Client(isolated);
        var account = await AccountJourney.RegisterAsync(isolated, client, Ct);
        var organizationId = await OrganizationJourney.StageAsync(isolated, account, "Quota before migration", Ct);
        var binding = new AssistantFinancialBinding(account.UserId, Access.Business, organizationId);
        var reserved = await ReserveAsync(isolated, binding);
        var invoked = await ReserveAsync(isolated, binding);
        var began = await WriteAsync(isolated, binding, services => new AssistantUsageWriter(services.GetRequiredService<IAssistantFinanceRepository>(),
            Clock(NowUtc)).BeginCallAsync(binding, invoked, 1, false, Ct));
        Assert.True(began.IsSuccess, began.Error.Code);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(isolated.AdminConnectionString);
        options.UseOpenIddict<Guid>();
        await using var db = new ApplicationDbContext(options.Options, new NoTenant());
        var migrator = db.GetService<IMigrator>();
        const string previous = "20261125053404_AssistantProviderIssue";
        const string current = "20261125105915_AssistantEngineVersionAndDailyQuota";
        // Lo que había antes de la migración: un solo cupo de la persona en su día UTC, sin organización. Sin cupos de una
        // organización ni turnos de otro motor, el Down vuelve atrás.
        await db.Database.ExecuteSqlAsync($"""
            UPDATE platform."AssistantPlatformKeyDailyQuotas" SET "OrganizationId" = NULL WHERE "UserId" = {account.UserId}
            """, Ct);
        await migrator.MigrateAsync(previous, Ct);
        Assert.DoesNotContain(current, await db.Database.GetAppliedMigrationsAsync(Ct));

        await migrator.MigrateAsync(current, Ct);
        var quotas = await QuotasAsync(isolated, account.UserId);
        Assert.Equal(2, quotas.Length);
        var organization = Assert.Single(quotas, row => row.OrganizationId == organizationId);
        Assert.Equal((1, 1), (organization.ReservedQueries, organization.ConsumedQueries));
        var outside = Assert.Single(quotas, row => row.OrganizationId is null);
        Assert.Equal((0, 0), (outside.ReservedQueries, outside.ConsumedQueries));

        foreach (var operationId in new[] { reserved, invoked })
        {
            var completed = await WriteAsync(isolated, binding, services => new AssistantUsageWriter(services.GetRequiredService<IAssistantFinanceRepository>(),
                Clock(NowUtc)).CompleteAsync(binding, operationId, Ct));
            Assert.True(completed.IsSuccess, completed.Error.Code);
        }
        organization = Assert.Single(await QuotasAsync(isolated, account.UserId), row => row.OrganizationId == organizationId);
        Assert.Equal((0, 1), (organization.ReservedQueries, organization.ConsumedQueries));
    }

    private static async Task<Guid> ReserveAsync(ApiFactory factory, AssistantFinancialBinding binding)
    {
        var reserved = await WriteAsync(factory, binding, services => new AssistantLimitGuard(services.GetRequiredService<IAssistantFinanceRepository>(),
                services.GetRequiredService<ITimeZoneService>(), services.GetRequiredService<AssistantQuotaDayResolver>(), Clock(NowUtc), services.GetRequiredService<IAssistantFinanceReader>())
            .ReserveAsync(new(Guid.CreateVersion7(), binding, AssistantMode.PlatformKey, 50, Usd(1000), "UTC", Price(), null), Ct));
        Assert.True(reserved.IsSuccess, reserved.Error.Code);
        return reserved.Value.Id;
    }

    private static async Task<AssistantPlatformKeyDailyQuota[]> QuotasAsync(ApiFactory factory, Guid userId)
    {
        using var reading = factory.Services.CreateScope();
        return await reading.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformKeyDailyQuota>().AsNoTracking()
            .Where(row => row.UserId == userId).ToArrayAsync(Ct);
    }

    private static async Task<Result<T>> WriteAsync<T>(ApiFactory factory, AssistantFinancialBinding binding, Func<IServiceProvider, Task<Result<T>>> work)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
        return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => work(services), CommitPolicy.OnSuccess, Ct);
    }

    private static async Task<Result> WriteAsync(ApiFactory factory, AssistantFinancialBinding binding, Func<IServiceProvider, Task<Result>> work)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(binding.ScopeTenantId!.Value);
        return await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => work(services), CommitPolicy.OnSuccess, Ct);
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No tenant.");
    }
}
