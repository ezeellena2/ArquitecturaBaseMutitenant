using System.Text.RegularExpressions;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class IdentityStorageAccessTests
{
    private static readonly HashSet<string> AllowedPersistenceAdapters = new(StringComparer.Ordinal)
    {
        "Repositories/UserRepository.cs",
        "Repositories/LoginMethodRepository.cs",
        "Repositories/LoginCodeRepository.cs",
        "Repositories/LoginAuditRepository.cs",
        "Repositories/LegalRepository.cs",
        "Readers/LoginMethodReader.cs",
        "Readers/MemberReader.cs",
    };

    [Fact]
    public void Only_declared_persistence_adapters_read_global_identity_tables()
    {
        var persistence = Path.Combine(SolutionRoot.FullPath, "src",
            "ArquitecturaBaseMultitenant.Infrastructure", "Persistence");
        var files = Directory.EnumerateFiles(persistence, "*.cs", SearchOption.AllDirectories)
            .Where(file => file.Contains(Path.DirectorySeparatorChar + "Repositories" + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)
                || file.Contains(Path.DirectorySeparatorChar + "Readers" + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal));

        var readers = files.Where(file => GlobalIdentityDbSet().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(persistence, file).Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(AllowedPersistenceAdapters.Order(StringComparer.Ordinal), readers.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Global_login_method_lookup_by_value_lives_only_in_identity()
    {
        var persistence = Path.Combine(SolutionRoot.FullPath, "src",
            "ArquitecturaBaseMultitenant.Infrastructure", "Persistence");
        var files = Directory.EnumerateFiles(persistence, "*.cs", SearchOption.AllDirectories)
            .Where(file => file.Contains(Path.DirectorySeparatorChar + "Repositories" + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal)
                || file.Contains(Path.DirectorySeparatorChar + "Readers" + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal));

        Assert.DoesNotContain(files, file => GlobalMethodValueLookup().IsMatch(File.ReadAllText(file)));
    }

    [GeneratedRegex(@"\b(?:context|dbContext)\.(?:Users|LoginMethods|LoginCodes|LoginAudits|LegalAcceptances)\b")]
    private static partial Regex GlobalIdentityDbSet();

    [GeneratedRegex(@"\bmethod\.(?:Value|NormalizedValue)\s*==")]
    private static partial Regex GlobalMethodValueLookup();
}
