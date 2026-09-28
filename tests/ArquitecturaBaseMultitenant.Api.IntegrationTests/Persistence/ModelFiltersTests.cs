using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class ModelFiltersTests
{
    [Fact]
    public void Each_isolation_class_has_its_named_filter_and_soft_delete_is_independent()
    {
        using var context = CreateContext(Guid.NewGuid());

        Assert.Equal(["SoftDelete", "Tenant"], FilterNames<Widget>(context).Order());
        Assert.Equal(["Public"], FilterNames<Poster>(context));
        Assert.Equal(["Parties"], FilterNames<Deal>(context));
        Assert.Empty(FilterNames<Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey>(context));
    }

    [Fact]
    public void Filters_reference_the_current_tenant_without_opening_a_connection()
    {
        using var context = CreateContext(Guid.NewGuid());

        var privateSql = context.Set<Widget>().Select(widget => widget.Id).ToQueryString();
        var publicSql = context.Set<Poster>().Select(poster => poster.Id).ToQueryString();
        var sharedSql = context.Set<Deal>().Select(deal => deal.Id).ToQueryString();
        var withDeletedSql = context.Set<Widget>().IgnoreQueryFilters(["SoftDelete"])
            .Select(widget => widget.Id).ToQueryString();

        Assert.Contains("TenantId", privateSql, StringComparison.Ordinal);
        Assert.Contains("IsDeleted", privateSql, StringComparison.Ordinal);
        Assert.Contains("IsPublished", publicSql, StringComparison.Ordinal);
        Assert.Contains("BusinessTenantId", publicSql, StringComparison.Ordinal);
        Assert.Contains("ConsumerTenantId", sharedSql, StringComparison.Ordinal);
        Assert.Contains("BusinessTenantId", sharedSql, StringComparison.Ordinal);
        Assert.Contains("TenantId", withDeletedSql, StringComparison.Ordinal);
        Assert.DoesNotContain("IsDeleted", withDeletedSql, StringComparison.Ordinal);
    }

    [Fact]
    public void Reused_model_reads_the_tenant_from_each_context_instance()
    {
        var firstTenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var secondTenant = Guid.Parse("22222222-2222-4222-8222-222222222222");
        using var first = CreateContext(firstTenant);
        using var second = CreateContext(secondTenant);

        Assert.Same(first.Model, second.Model);
        var firstSql = first.Set<Widget>().Select(widget => widget.Id).ToQueryString();
        var secondSql = second.Set<Widget>().Select(widget => widget.Id).ToQueryString();
        Assert.Contains(firstTenant.ToString(), firstSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secondTenant.ToString(), firstSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(secondTenant.ToString(), secondSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(firstTenant.ToString(), secondSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unclassified_entity_is_rejected_even_in_a_global_schema()
    {
        var builder = new ModelBuilder(new ConventionSet());
        builder.Entity<Unclassified>().ToTable("Unclassified", Schemas.Platform);

        Assert.Throws<InvalidOperationException>(() => TenantIsolationModelValidator.Validate(builder.Model));
    }

    [Fact]
    public void Tenant_entity_requires_both_its_schema_and_named_filter()
    {
        var missingFilter = new ModelBuilder(new ConventionSet());
        missingFilter.Entity<Widget>().ToTable("Widgets", Schemas.Tenant);
        Assert.Throws<InvalidOperationException>(() => TenantIsolationModelValidator.Validate(missingFilter.Model));

        var wrongSchema = new ModelBuilder(new ConventionSet());
        wrongSchema.Entity<Widget>().ToTable("Widgets", Schemas.Platform)
            .HasQueryFilter("Tenant", widget => widget.TenantId != Guid.Empty);
        Assert.Throws<InvalidOperationException>(() => TenantIsolationModelValidator.Validate(wrongSchema.Model));
    }

    private static string[] FilterNames<TEntity>(DbContext context) where TEntity : class =>
        context.Model.FindEntityType(typeof(TEntity))!.GetDeclaredQueryFilters()
            .Select(filter => filter.Key!).ToArray();

    private static IsolationApplicationDbContext CreateContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<IsolationApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_filter_test")
            .ReplaceService<IModelCustomizer, IsolationModelCustomizer>()
            .Options;
        return new IsolationApplicationDbContext(options, new TestTenantContext(tenantId));
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => tenantId ?? throw new InvalidOperationException("No tenant.");
    }

    private sealed class Unclassified
    {
        public Guid Id { get; set; }
    }
}
