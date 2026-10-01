using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// Comprueba esquemas, columnas y unicidad del modelo global de identidad. Protege la separación entre
/// cuentas globales y datos privados de una organización.
/// </summary>
public sealed class IdentityModelTests
{
    [Fact]
    public void Identity_and_login_tables_are_global_and_methods_have_unique_type_value()
    {
        using var context = CreateContext();
        var model = context.Model;
        var user = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType>(
            model.FindEntityType(typeof(ApplicationUser)));
        var method = Assert.IsAssignableFrom<Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType>(
            model.FindEntityType(typeof(LoginMethod)));

        Assert.Equal(Schemas.Identity, user.GetSchema());
        Assert.Equal("AspNetUsers", user.GetTableName());
        Assert.DoesNotContain(user.GetIndexes(), index => index.IsUnique &&
            index.Properties.Any(property => property.Name is "Email" or "NormalizedEmail"));
        Assert.Equal(Schemas.Identity, method.GetSchema());
        Assert.Equal("LoginMethods", method.GetTableName());
        Assert.Contains(method.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual(["Type", "Value"]));
    }

    [Fact]
    public void Codes_and_audit_use_identity_schema_and_utc_timestamp_columns()
    {
        using var context = CreateContext();
        var code = context.Model.FindEntityType(typeof(LoginCode));
        var audit = context.Model.FindEntityType(typeof(LoginAudit));

        Assert.NotNull(code);
        Assert.NotNull(audit);
        Assert.Equal(Schemas.Identity, code.GetSchema());
        Assert.Equal(Schemas.Identity, audit.GetSchema());
        Assert.Equal("timestamp with time zone", code.FindProperty(nameof(LoginCode.ExpiresAtUtc))?.GetColumnType());
        Assert.Equal("timestamp with time zone", audit.FindProperty(nameof(LoginAudit.OccurredAtUtc))?.GetColumnType());
    }

    [Fact]
    public void Identity_auxiliary_tables_are_mapped_once_to_identity()
    {
        using var context = CreateContext();

        Assert.Equal(Schemas.Identity,
            context.Model.FindEntityType(typeof(IdentityUserClaim<Guid>))?.GetSchema());
        Assert.Equal(Schemas.Identity,
            context.Model.FindEntityType(typeof(IdentityUserLogin<Guid>))?.GetSchema());
        Assert.Equal(Schemas.Identity,
            context.Model.FindEntityType(typeof(IdentityUserToken<Guid>))?.GetSchema());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=identity_model_test")
            .Options;
        return new ApplicationDbContext(options, new EmptyTenantContext());
    }

    private sealed class EmptyTenantContext : ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request.ITenantContext
    {
        public Guid? TenantId => null;
        public ArquitecturaBaseMultitenant.Domain.Tenancy.TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}
