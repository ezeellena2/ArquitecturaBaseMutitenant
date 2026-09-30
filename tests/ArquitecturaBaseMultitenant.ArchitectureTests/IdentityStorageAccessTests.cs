using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class IdentityStorageAccessTests
{
    private const string Infrastructure = "ArquitecturaBaseMultitenant.Infrastructure.";
    private static readonly string[] AllowedReaders =
    [
        Infrastructure + "Identity.SignInService",
        Infrastructure + "Identity.UserLookup",
        Infrastructure + "Identity.UserStatusReader",
        Infrastructure + "Persistence.Readers.LoginMethodReader",
        Infrastructure + "Persistence.Readers.LegalReader",
        Infrastructure + "Persistence.Readers.LoginMethodMembershipReader",
        Infrastructure + "Persistence.Readers.MemberReader",
        Infrastructure + "Persistence.Readers.UserTenantAccessReader",
        Infrastructure + "Persistence.Repositories.LegalRepository",
        Infrastructure + "Persistence.Repositories.LoginAuditRepository",
        Infrastructure + "Persistence.Repositories.LoginCodeRepository",
        Infrastructure + "Persistence.Repositories.LoginMethodRepository",
        Infrastructure + "Persistence.Repositories.UserRepository",
        Infrastructure + "Persistence.Seed.PlatformSeeder",
    ];

    private static readonly HashSet<string> IdentitySetGetters = new(StringComparer.Ordinal)
    {
        "get_Users", "get_LoginMethods", "get_LoginCodes", "get_LoginAudits",
        "get_LegalAcceptances", "get_UserTenantAccesses",
    };

    private static readonly HashSet<string> IdentityEntityTypes = new(StringComparer.Ordinal)
    {
        "ArquitecturaBaseMultitenant.Infrastructure.Identity.ApplicationUser",
        "ArquitecturaBaseMultitenant.Domain.Authentication.LoginMethod",
        "ArquitecturaBaseMultitenant.Domain.Authentication.LoginCode",
        "ArquitecturaBaseMultitenant.Domain.Authentication.LoginAudit",
        "ArquitecturaBaseMultitenant.Domain.Legal.LegalAcceptance",
        "ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex.UserTenantAccess",
    };

    [Fact]
    public void Only_declared_adapters_read_global_identity_sets_through_getters_or_Set()
    {
        var readers = IdentityCalls(ArchitectureIl.Calls(typeof(ApplicationDbContext).Assembly))
            .Where(call => call.Owner != typeof(ApplicationDbContext).FullName)
            .Select(call => call.Owner)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(AllowedReaders.Order(StringComparer.Ordinal), readers);
    }

    [Fact]
    public void Detector_catches_underscore_context_and_generic_Set_without_matching_other_sets()
    {
        var calls = IdentityCalls(ArchitectureIl.Calls(typeof(IdentityStorageAccessTests).Assembly))
            .Where(call => call.Owner == typeof(IdentityStorageProbe).FullName)
            .ToArray();

        Assert.Contains(calls, call => call.OwnerMethod == nameof(IdentityStorageProbe.ReadUsers)
            && call.Method == "get_Users");
        Assert.Contains(calls, call => call.OwnerMethod == nameof(IdentityStorageProbe.ReadLoginMethods)
            && call.Method == "Set");
        Assert.DoesNotContain(calls, call => call.OwnerMethod == nameof(IdentityStorageProbe.ReadTenants));
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

    private static ArchitectureIl.Call[] IdentityCalls(IEnumerable<ArchitectureIl.Call> calls) =>
        [.. calls.Where(IsIdentityCall)];

    private static bool IsIdentityCall(ArchitectureIl.Call call)
    {
        if (IdentitySetGetters.Contains(call.Method))
        {
            return call.DeclaringType == typeof(ApplicationDbContext).FullName
                || call.Method == "get_Users" &&
                (call.DeclaringType.Contains("IdentityUserContext", StringComparison.Ordinal)
                    || call.DeclaringType.Contains("UserManager", StringComparison.Ordinal));
        }

        return call.Method == "Set"
            && call.DeclaringType.Contains("DbContext", StringComparison.Ordinal)
            && call.GenericArguments is { } arguments
            && arguments.Any(IdentityEntityTypes.Contains);
    }

    [GeneratedRegex(@"\bmethod\.(?:Value|NormalizedValue)\s*==")]
    private static partial Regex GlobalMethodValueLookup();
}

internal sealed class IdentityStorageProbe(ApplicationDbContext context)
{
    public IQueryable<ApplicationUser> ReadUsers() => context.Users;

    public IQueryable<LoginMethod> ReadLoginMethods() => context.Set<LoginMethod>();

    public IQueryable<ArquitecturaBaseMultitenant.Domain.Tenancy.Tenant> ReadTenants() => context.Tenants;
}
