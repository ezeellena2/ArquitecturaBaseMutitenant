using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class DbContextModelTests
{
    [Fact]
    public void Schema_names_are_centralized_for_the_five_data_classes()
    {
        using var context = CreateContext();
        Assert.Equal(["platform", "identity", "tenant", "public_site", "engagement"], Schemas.All);
        Assert.Equal(Schemas.Platform, context.Model.GetDefaultSchema());
    }

    [Fact]
    public void E2_model_maps_data_protection_in_platform_without_identity_tables()
    {
        using var context = CreateContext();
        var model = context.Model;
        var keys = model.FindEntityType(typeof(DataProtectionKey));

        Assert.NotNull(keys);
        Assert.Equal("platform", keys.GetSchema());
        Assert.Equal("DataProtectionKeys", keys.GetTableName());
        Assert.DoesNotContain(model.GetEntityTypes(), entity => entity.ClrType == typeof(ApplicationUser));
        Assert.DoesNotContain(model.GetEntityTypes(), entity =>
            entity.GetTableName()?.StartsWith("AspNet", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(model.GetEntityTypes(), entity =>
            entity.ClrType.Namespace == "Microsoft.AspNetCore.Identity");
    }

    [Fact]
    public void Infrastructure_registers_one_db_context_and_one_tenant_context_per_scope()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            [new KeyValuePair<string, string?>("ConnectionStrings:appdb", "Host=localhost;Database=model_test")])
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration, new TestHostEnvironment());

        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider;
        var second = secondScope.ServiceProvider;

        Assert.Same(first.GetRequiredService<ApplicationDbContext>(), first.GetRequiredService<ApplicationDbContext>());
        Assert.NotSame(first.GetRequiredService<ApplicationDbContext>(), second.GetRequiredService<ApplicationDbContext>());
        Assert.Same(first.GetRequiredService<ITenantContext>(), first.GetRequiredService<ITenantScope>());
        Assert.NotSame(first.GetRequiredService<ITenantContext>(), second.GetRequiredService<ITenantContext>());
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

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = nameof(DbContextModelTests);
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
