namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>La entidad Identity de EF no cruza al dominio, casos de uso ni HTTP.</summary>
public sealed class IdentityAccessTests
{
    [Fact]
    public void Global_access_reader_is_consumed_only_by_access_selection_and_me()
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
            "ArquitecturaBaseMultitenant.Infrastructure/Persistence/Seed/DevelopmentSeeder.cs",
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
            || relative == "Persistence/Repositories/UserRepository.cs"
            || relative == "Persistence/Readers/MemberReader.cs"
            || relative.StartsWith("Persistence/Readers/Platform/", StringComparison.Ordinal)
            || relative.StartsWith("Persistence/Seed/", StringComparison.Ordinal)
            || relative.StartsWith("Persistence/Migrations/", StringComparison.Ordinal)
            || relative == "Persistence/Rls/TenantIsolationModelValidator.cs";
    }

    private static bool IsGenerated(string file)
    {
        var relative = Path.GetRelativePath(SolutionRoot.FullPath, file).Replace('\\', '/');
        return relative.Contains("/obj/", StringComparison.Ordinal)
            || relative.Contains("/bin/", StringComparison.Ordinal);
    }
}
