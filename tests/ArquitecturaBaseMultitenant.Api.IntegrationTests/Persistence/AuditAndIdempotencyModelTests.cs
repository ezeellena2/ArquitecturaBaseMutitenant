using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Idempotency;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class AuditAndIdempotencyModelTests
{
    [Fact]
    public void Audit_entries_use_tenant_schema_jsonb_and_a_tenant_first_key()
    {
        using var context = CreateContext();
        var entry = context.Model.FindEntityType(typeof(AuditEntry));

        Assert.NotNull(entry);
        Assert.Equal(Schemas.Tenant, entry.GetSchema());
        Assert.Equal("AuditEntries", entry.GetTableName());
        Assert.Equal([nameof(AuditEntry.TenantId), nameof(AuditEntry.Id)],
            entry.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("jsonb", entry.FindProperty(nameof(AuditEntry.Changes))!.GetColumnType());
        Assert.Equal(typeof(string), entry.FindProperty(nameof(AuditEntry.Action))!.GetProviderClrType());
        Assert.Equal(typeof(string), entry.FindProperty(nameof(AuditEntry.ActorKind))!.GetProviderClrType());
        Assert.Contains(entry.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(AuditEntry.TenantId), nameof(AuditEntry.OccurredAtUtc), nameof(AuditEntry.Id)]));
    }

    [Fact]
    public void Idempotency_keys_use_platform_and_enforce_uniqueness_even_without_a_tenant()
    {
        using var context = CreateContext();
        var key = context.Model.FindEntityType(typeof(IdempotencyKey));
        var designKey = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(IdempotencyKey));

        Assert.NotNull(key);
        Assert.Equal(Schemas.Platform, key.GetSchema());
        Assert.Equal("IdempotencyKeys", key.GetTableName());
        Assert.Empty(key.GetDeclaredQueryFilters());
        Assert.True(key.FindProperty(nameof(IdempotencyKey.TenantId))!.IsNullable);
        Assert.True(key.FindProperty(nameof(IdempotencyKey.ResponseStatusCode))!.IsNullable);
        Assert.True(key.FindProperty(nameof(IdempotencyKey.ResponseBody))!.IsNullable);

        var unique = Assert.Single(key.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(IdempotencyKey.TenantId), nameof(IdempotencyKey.UserId), nameof(IdempotencyKey.Key)]));
        Assert.True(unique.IsUnique);
        var designUnique = Assert.Single(designKey!.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(IdempotencyKey.TenantId), nameof(IdempotencyKey.UserId), nameof(IdempotencyKey.Key)]));
        Assert.False(designUnique.GetAreNullsDistinct());
        Assert.Contains(key.GetIndexes(), index => index.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(IdempotencyKey.ExpiresAtUtc)]));
    }

    [Fact]
    public void Every_mapped_decimal_has_explicit_precision()
    {
        using var context = CreateContext();
        var decimals = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?));

        Assert.All(decimals, property => Assert.NotNull(property.GetPrecision()));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test")
            .Options;
        return new ApplicationDbContext(options, new EmptyTenantContext());
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}
