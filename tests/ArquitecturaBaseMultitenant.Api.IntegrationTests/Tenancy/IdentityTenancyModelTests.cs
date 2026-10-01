using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Tenancy;

/// <summary>
/// Comprueba que el catálogo de espacios sea global y sus membresías y preferencias sean privadas. Exige
/// filtros y claves con el espacio primero.
/// </summary>
public sealed class IdentityTenancyModelTests
{
    [Fact]
    public void Tenant_is_global_but_member_and_settings_are_private_with_named_filters()
    {
        using var context = CreateContext();
        var tenant = context.Model.FindEntityType(typeof(Tenant));
        var member = context.Model.FindEntityType(typeof(Member));
        var settings = context.Model.FindEntityType(typeof(TenantSettings));

        Assert.NotNull(tenant);
        Assert.NotNull(member);
        Assert.NotNull(settings);
        Assert.Equal(Schemas.Platform, tenant.GetSchema());
        Assert.Equal(Schemas.Tenant, member.GetSchema());
        Assert.Equal(Schemas.Tenant, settings.GetSchema());
        Assert.Empty(tenant.GetDeclaredQueryFilters());
        Assert.Equal("Tenant", Assert.Single(member.GetDeclaredQueryFilters()).Key);
        Assert.Equal("Tenant", Assert.Single(settings.GetDeclaredQueryFilters()).Key);
    }

    [Fact]
    public void Membership_and_settings_keys_keep_tenant_column_first()
    {
        using var context = CreateContext();
        var member = context.Model.FindEntityType(typeof(Member));
        var settings = context.Model.FindEntityType(typeof(TenantSettings));

        Assert.NotNull(member);
        Assert.NotNull(settings);
        Assert.Equal("TenantId", member.FindPrimaryKey()?.Properties[0].Name);
        Assert.Equal("TenantId", settings.FindPrimaryKey()?.Properties[0].Name);
        Assert.Contains(member.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "UserId"]));
        Assert.Contains(settings.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(["TenantId"]));
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=tenancy_model_test").Options;
        return new ApplicationDbContext(options, new EmptyTenantContext());
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}
