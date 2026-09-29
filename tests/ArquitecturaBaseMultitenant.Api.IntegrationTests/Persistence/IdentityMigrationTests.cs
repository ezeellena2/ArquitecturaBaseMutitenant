using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class IdentityMigrationTests
{
    [Fact]
    public void Forward_migration_creates_identity_legal_outbox_and_tenant_tables()
    {
        using var context = CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript();

        foreach (var table in new[] { "AspNetUsers", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens",
                     "LoginMethods", "LoginCodes", "LoginAudits", "LegalAcceptances" })
        {
            Assert.Contains($"CREATE TABLE identity.\"{table}\"", script, StringComparison.Ordinal);
        }

        foreach (var table in new[] { "Tenants", "PlatformSettings", "SecurityEvents", "OutboxMessages",
                     "LegalDocuments", "LegalDocumentContents" })
        {
            Assert.Contains($"CREATE TABLE platform.\"{table}\"", script, StringComparison.Ordinal);
        }

        Assert.Contains("CREATE TABLE tenant.\"Members\"", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE tenant.\"TenantSettings\"", script, StringComparison.Ordinal);
        Assert.Contains("IX_LoginMethods_Type_Value", script, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=identity_migration_test").Options;
        return new ApplicationDbContext(options, new EmptyTenantContext());
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}
