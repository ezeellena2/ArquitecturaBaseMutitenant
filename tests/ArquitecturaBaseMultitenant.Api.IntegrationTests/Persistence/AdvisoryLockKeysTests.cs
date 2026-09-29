using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class AdvisoryLockKeysTests
{
    private static readonly Guid TenantA = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
    private static readonly Guid TenantB = Guid.Parse("abcdef01-2345-6789-abcd-ef0123456789");
    private static readonly Guid ResourceId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public void Lock_key_contains_tenant_resource_and_id_with_a_stable_format()
    {
        Assert.Equal(
            "t:0123456789abcdef0123456789abcdef:lock:inventory:11111111222233334444555555555555",
            AdvisoryLockKeys.For(TenantA, "inventory", ResourceId));
        Assert.Equal(
            "t:0123456789abcdef0123456789abcdef:lock:login-code:ana@example.com",
            AdvisoryLockKeys.For(TenantA, "login-code", "ana@example.com"));
    }

    [Fact]
    public void Reference_seed_has_one_global_lock_key()
    {
        Assert.Equal("p:ref:seed", AdvisoryLockKeys.ReferenceDataSeed);
    }

    [Fact]
    public void Different_tenants_never_share_a_resource_lock()
    {
        Assert.NotEqual(
            AdvisoryLockKeys.For(TenantA, "inventory", ResourceId),
            AdvisoryLockKeys.For(TenantB, "inventory", ResourceId));
    }

    [Fact]
    public void Invalid_tenant_resource_or_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => AdvisoryLockKeys.For(Guid.Empty, "inventory", ResourceId));
        Assert.Throws<ArgumentException>(() => AdvisoryLockKeys.For(TenantA, " ", ResourceId));
        Assert.Throws<ArgumentException>(() => AdvisoryLockKeys.For(TenantA, "inventory", Guid.Empty));
        Assert.Throws<ArgumentException>(() => AdvisoryLockKeys.For(TenantA, "inventory", " "));
    }

    [Fact]
    public async Task Advisory_lock_requires_an_open_transaction_even_for_no_keys()
    {
        await using var context = CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.AcquireAdvisoryLocksAsync([], TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Transaction_guards_detect_an_absent_transaction()
    {
        using var context = CreateContext();

        Assert.Throws<InvalidOperationException>(() => context.RequireTransaction());
        context.RequireNoTransaction();
    }

    private static DbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseNpgsql("Host=localhost;Database=not_used")
            .Options;
        return new DbContext(options);
    }
}

[Collection(ApiTestGroup.Name)]
public sealed class AdvisoryLockTransactionTests(ApiFactory factory)
{
    [Fact]
    public async Task Lock_is_held_until_commit_and_another_tenant_can_lock_the_same_resource()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var keyA = AdvisoryLockKeys.For(tenantA, "inventory", resourceId);
        var keyB = AdvisoryLockKeys.For(tenantB, "inventory", resourceId);
        var ct = TestContext.Current.CancellationToken;
        var options = new DbContextOptionsBuilder<DbContext>()
            .UseNpgsql(factory.RuntimeConnectionString)
            .Options;

        await using var context = new DbContext(options);
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        context.RequireTransaction();
        Assert.Throws<InvalidOperationException>(() => context.RequireNoTransaction());
        await context.AcquireAdvisoryLocksAsync([keyA], ct);

        await using var probe = new NpgsqlConnection(factory.RuntimeConnectionString);
        await probe.OpenAsync(ct);
        Assert.False(await CanAcquireAsync(probe, keyA, ct));
        Assert.True(await CanAcquireAsync(probe, keyB, ct));

        await transaction.CommitAsync(ct);
        Assert.True(await CanAcquireAsync(probe, keyA, ct));
    }

    private static async Task<bool> CanAcquireAsync(NpgsqlConnection connection, string key, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_try_advisory_xact_lock(hashtextextended(@key, 0))", connection);
        command.Parameters.AddWithValue("key", key);
        return (bool)(await command.ExecuteScalarAsync(ct))!;
    }
}
