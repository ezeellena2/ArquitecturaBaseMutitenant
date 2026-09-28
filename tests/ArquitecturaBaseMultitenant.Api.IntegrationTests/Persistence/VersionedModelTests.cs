using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Conventions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Persistence;

public sealed class VersionedModelTests
{
    [Fact]
    public void Versioned_entities_map_the_Postgres_xmin_as_a_generated_concurrency_token()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(VersionedSample));
        var version = entity!.FindProperty(nameof(VersionedSample.Version));

        Assert.NotNull(version);
        Assert.Equal("xmin", version.GetColumnName());
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
    }

    [Fact]
    public void Email_properties_use_the_canonical_value_converter_and_254_character_limit()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(VersionedSample));
        var email = entity!.FindProperty(nameof(VersionedSample.Email));
        var converter = email!.GetValueConverter();

        Assert.NotNull(converter);
        Assert.Equal(typeof(string), converter.ProviderClrType);
        Assert.Equal(Email.MaxLength, email.GetMaxLength());

        var value = Email.Create("  Persona@MÜNICH.DE  ").Value;
        var stored = Assert.IsType<string>(converter.ConvertToProvider(value));
        Assert.Equal(value.Value, stored);
        var hydrated = Assert.IsType<Email>(converter.ConvertFromProvider(stored));
        Assert.Equal(value, hydrated);
    }

    [Fact]
    public void Application_context_applies_both_conventions_to_isolation_widgets()
    {
        var options = new DbContextOptionsBuilder<IsolationApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=model_test")
            .ReplaceService<IModelCustomizer, IsolationModelCustomizer>()
            .Options;
        using var context = new IsolationApplicationDbContext(options, new EmptyTenantContext());
        var widget = context.Model.FindEntityType(typeof(Widget));

        Assert.NotNull(widget);
        Assert.Equal("xmin", widget.FindProperty(nameof(Widget.Version))!.GetColumnName());
        Assert.NotNull(widget.FindProperty("Email")?.GetValueConverter());
    }

    private static SampleContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SampleContext>()
            .UseNpgsql("Host=localhost;Database=model_test")
            .Options;
        return new SampleContext(options);
    }

    private sealed class SampleContext(DbContextOptions<SampleContext> options) : DbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            EmailConvention.Configure(configurationBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<VersionedSample>().HasKey(entity => entity.Id);
            VersionedConvention.Apply(modelBuilder);
        }
    }

    private sealed class VersionedSample : IVersioned
    {
        public Guid Id { get; private set; }
        public uint Version { get; private set; }
        public Email Email { get; private set; } = null!;
    }

    private sealed class EmptyTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public TenantKind? TenantKind => null;
        public Guid RequiredTenantId => throw new InvalidOperationException("No active tenant.");
    }
}
