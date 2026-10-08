using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>La entidad Identity de EF no cruza al dominio, casos de uso ni HTTP.</summary>
public sealed class IdentityAccessTests
{
    [Fact]
    public void Global_access_reader_is_consumed_only_by_access_selection_tenant_validation_seed_and_me()
    {
        var source = Path.Combine(SolutionRoot.FullPath, "src");
        var consumers = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file))
            .Where(file => File.ReadLines(file).Any(line =>
                line.Contains("IUserTenantAccessReader", StringComparison.Ordinal)))
            .Select(file => Path.GetRelativePath(source, file).Replace('\\', '/'))
            .Where(path => path is not
                ("ArquitecturaBaseMultitenant.Application/Interfaces/Persistence/IUserTenantAccessReader.cs"
                or "ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/UserTenantAccessReader.cs"
                or "ArquitecturaBaseMultitenant.Infrastructure/Persistence/PersistenceRegistration.cs"))
            .ToArray();

        Assert.Equal([
            "ArquitecturaBaseMultitenant.Application/Services/Auth/ConnectService.cs",
            "ArquitecturaBaseMultitenant.Application/Services/Profile/ProfileSnapshotBuilder.cs",
            "ArquitecturaBaseMultitenant.Infrastructure/Caching/AccessStatusCache.cs",
        ], consumers.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ApplicationUser_is_referenced_only_by_declared_infrastructure_adapters()
    {
        var source = Path.Combine(SolutionRoot.FullPath, "src");
        var references = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGenerated(file))
            .SelectMany(file => File.ReadLines(file)
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)
                    && line.Contains("ApplicationUser", StringComparison.Ordinal))
                .Select(_ => Path.GetRelativePath(source, file).Replace('\\', '/')))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(references);
        Assert.DoesNotContain(references, path => !IsAllowed(path));
    }

    [Theory]
    [InlineData("ArquitecturaBaseMultitenant.Application/Services/Auth/Fake.cs", false)]
    [InlineData("ArquitecturaBaseMultitenant.Api/Controllers/Auth/Fake.cs", false)]
    [InlineData("ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/Fake.cs", false)]
    [InlineData("ArquitecturaBaseMultitenant.Infrastructure/Persistence/Repositories/UserRepository.cs", true)]
    [InlineData("ArquitecturaBaseMultitenant.Infrastructure/Persistence/Readers/Platform/Fake.cs", false)]
    [InlineData("ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/DatabaseSeeder.cs", false)]
    [InlineData("ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/PlatformSeeder.cs", true)]
    public void Identity_boundary_detector_rejects_unlisted_paths(string path, bool allowed) =>
        Assert.Equal(allowed, IsAllowed(path));

    private static bool IsAllowed(string path)
    {
        const string root = "ArquitecturaBaseMultitenant.Infrastructure/";
        if (!path.StartsWith(root, StringComparison.Ordinal)) return false;
        var relative = path[root.Length..];
        return relative.StartsWith("Identity/", StringComparison.Ordinal)
            || relative == "Persistence/ApplicationDbContext.cs"
            || relative.StartsWith("Persistence/Configurations/Identity/", StringComparison.Ordinal)
            || relative == "Persistence/Configurations/Tenant/MemberConfiguration.cs"
            || relative == "Persistence/Configurations/Tenant/InvitationConfiguration.cs"
            || relative == "Persistence/Configurations/Platform/PlatformPermissionAssignmentConfiguration.cs"
            || relative == "Persistence/Repositories/UserRepository.cs"
            || relative == "Persistence/Readers/MemberReader.cs"
            || relative == "Persistence/Seed/PlatformSeeder.cs"
            || relative.StartsWith("Persistence/Migrations/", StringComparison.Ordinal)
            || relative == "Persistence/Rls/TenantIsolationModelValidator.cs"
            || IsContributedIdentityConfiguration(relative);
    }

    private static bool IsContributedIdentityConfiguration(string path)
    {
        if (!path.StartsWith("Modules/", StringComparison.Ordinal)) return false;
        var contributors = PersistenceModelContributors.Discover();
        return typeof(ApplicationDbContext).Assembly.GetTypes().Any(type =>
            IsDeclaredIdentityConfiguration(type, contributors)
            && path == type.FullName!["ArquitecturaBaseMultitenant.Infrastructure.".Length..]
                .Replace('.', '/') + ".cs");
    }

    private static bool IsDeclaredIdentityConfiguration(Type type, IEnumerable<IPersistenceModelContributor> contributors) =>
        type is { IsClass: true, IsAbstract: false }
        && type.GetInterfaces().Any(contract => contract.IsGenericType
            && contract.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)
            && contributors.Any(contributor => contributor.ConfigurationNamespace == type.Namespace
                && contributor.GlobalEntities.TryGetValue(contract.GenericTypeArguments[0], out var schema)
                && schema == Schemas.Identity));

    [Fact]
    public void Contributed_identity_configuration_requires_contract_namespace_and_explicit_identity_entity()
    {
        var configuration = typeof(ConfigurationProbe);
        Assert.True(IsDeclaredIdentityConfiguration(configuration, [new ContributorProbe(configuration.Namespace!, Schemas.Identity)]));
        Assert.False(IsDeclaredIdentityConfiguration(configuration, [new ContributorProbe("Other", Schemas.Identity)]));
        Assert.False(IsDeclaredIdentityConfiguration(configuration, [new ContributorProbe(configuration.Namespace!, Schemas.Platform)]));
        Assert.False(IsDeclaredIdentityConfiguration(configuration, []));
        Assert.False(IsDeclaredIdentityConfiguration(typeof(IdentityAccessTests), [new ContributorProbe(configuration.Namespace!, Schemas.Identity)]));
        Assert.False(IsAllowed("ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/Persistence/Fake.cs"));
        Assert.False(IsAllowed("ArquitecturaBaseMultitenant.Infrastructure/Modules/WhatsApp/Services/Fake.cs"));
    }

    private sealed class ConfigurationProbe : IEntityTypeConfiguration<string>
    {
        public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<string> builder) => throw new NotSupportedException();
    }

    private sealed class ContributorProbe(string configurationNamespace, string schema) : IPersistenceModelContributor
    {
        public string ConfigurationNamespace => configurationNamespace;
        public IReadOnlyDictionary<Type, string> GlobalEntities { get; } = new Dictionary<Type, string> { [typeof(string)] = schema };
        public void Configure(ModelBuilder builder) => throw new NotSupportedException();
    }

    private static bool IsGenerated(string file)
    {
        var relative = Path.GetRelativePath(SolutionRoot.FullPath, file).Replace('\\', '/');
        return relative.Contains("/obj/", StringComparison.Ordinal)
            || relative.Contains("/bin/", StringComparison.Ordinal);
    }
}
