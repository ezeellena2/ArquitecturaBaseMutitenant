using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

[Collection(ApiTestGroup.Name)]
public sealed class UnitOfWorkTests(ApiFactory factory)
{
    [Fact]
    public async Task OnSuccess_commits_one_save_with_transaction_local_tenant()
    {
        var tenant = NewTenant();
        var saves = new SaveCounter();
        await using var context = CreateContext(tenant, saves);
        var unitOfWork = CreateUnitOfWork(context, tenant);
        var widget = new Widget(tenant.RequiredTenantId, "committed");

        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            Assert.Equal(tenant.RequiredTenantId.ToString("D"), await CurrentTenantAsync(context, ct));
            context.Set<Widget>().Add(widget);
            return Result.Success();
        }, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, saves.Count);
        Assert.Equal(1, await CountAsync(tenant.RequiredTenantId, widget.Id));
    }

    [Fact]
    public async Task OnSuccess_failure_rolls_back_and_clears_tracker()
    {
        var tenant = NewTenant();
        await using var context = CreateContext(tenant);
        var widget = new Widget(tenant.RequiredTenantId, "rolled back");

        var result = await CreateUnitOfWork(context, tenant).ExecuteInTransactionAsync(ct =>
        {
            context.Set<Widget>().Add(widget);
            return Task.FromResult(Result.Failure(Error.Conflict("Test.Rejected", "Rejected.")));
        }, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(0, await CountAsync(tenant.RequiredTenantId, widget.Id));
    }

    [Fact]
    public async Task OnAnyResult_commits_even_when_business_result_fails()
    {
        var tenant = NewTenant();
        await using var context = CreateContext(tenant);
        var widget = new Widget(tenant.RequiredTenantId, "saved failure");

        var result = await CreateUnitOfWork(context, tenant).ExecuteInTransactionAsync(ct =>
        {
            context.Set<Widget>().Add(widget);
            return Task.FromResult(Result.Failure(Error.Conflict("Test.Recorded", "Recorded.")));
        }, CommitPolicy.OnAnyResult, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(1, await CountAsync(tenant.RequiredTenantId, widget.Id));
    }

    [Fact]
    public async Task Nested_boundary_is_rejected()
    {
        var tenant = NewTenant();
        await using var context = CreateContext(tenant);
        var unitOfWork = CreateUnitOfWork(context, tenant);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                unitOfWork.ExecuteInTransactionAsync(
                    _ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, ct));
            return Result.Success();
        }, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Unique_violation_is_translated_and_tracker_is_cleared()
    {
        var tenant = NewTenant();
        await using var context = CreateContext(tenant);
        var unitOfWork = CreateUnitOfWork(context, tenant);
        var widget = new Widget(tenant.RequiredTenantId, "unique");
        await unitOfWork.ExecuteInTransactionAsync(ct =>
        {
            context.Set<Widget>().Add(widget);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        context.Set<Widget>().Add(widget);

        var exception = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() =>
            unitOfWork.ExecuteInTransactionAsync(
                _ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess,
                TestContext.Current.CancellationToken));

        Assert.Equal("PK_Widgets", exception.ConstraintName);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Equal(1, await CountAsync(tenant.RequiredTenantId, widget.Id));
    }

    [Fact]
    public async Task Exception_after_a_save_rolls_back_the_row_and_clears_tracker()
    {
        var tenant = NewTenant();
        await using var context = CreateContext(tenant);
        var widget = new Widget(tenant.RequiredTenantId, "save then fail");
        var probe = new CommitFailureProbe();
        var failing = new FailingCommitUnitOfWork(CreateUnitOfWork(context, tenant), context, probe);

        await Assert.ThrowsAsync<ExpectedCommitFailure>(() => failing.ExecuteInTransactionAsync(ct =>
        {
            context.Set<Widget>().Add(widget);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, TestContext.Current.CancellationToken));

        Assert.True(probe.RolledBackBeforeLeaving);
        Assert.Equal(0, await CountAsync(tenant.RequiredTenantId, widget.Id));
    }

    private static TenantContext NewTenant()
    {
        var tenant = new TenantContext();
        tenant.SetFromAccess(Guid.NewGuid(), TenantKind.Personal);
        return tenant;
    }

    private IsolationApplicationDbContext CreateContext(TenantContext tenant, SaveCounter? saves = null)
    {
        var builder = new DbContextOptionsBuilder<IsolationApplicationDbContext>()
            .UseNpgsql(factory.RuntimeConnectionString)
            .ReplaceService<IModelCustomizer, IsolationModelCustomizer>()
            .AddInterceptors(new AuditableEntityInterceptor(new SystemCurrentUser(), TimeProvider.System));
        if (saves is not null)
        {
            builder.AddInterceptors(saves);
        }

        return new IsolationApplicationDbContext(builder.Options, tenant);
    }

    private static UnitOfWork CreateUnitOfWork(IsolationApplicationDbContext context, TenantContext tenant) =>
        new(context, tenant, NullLogger<UnitOfWork>.Instance);

    private static async Task<string?> CurrentTenantAsync(
        IsolationApplicationDbContext context, CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT current_setting('app.tenant_id', true)";
        return (string?)await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<long> CountAsync(Guid tenantId, Guid id)
    {
        await using var connection = await RuntimeRoleConnection.OpenAsync(
            factory.RuntimeConnectionString, tenantId, TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM tenant.\"Widgets\" WHERE \"Id\" = @id", connection);
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private sealed class SaveCounter : SaveChangesInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
