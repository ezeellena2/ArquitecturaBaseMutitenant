using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Detecta entradas a un alcance de organización fuera de los adaptadores y altas autorizados. Protege el
/// punto donde se cambia el contexto de aislamiento.
/// </summary>
public sealed partial class TenantScopeUsageTests
{
    private const string TenantContext = "ArquitecturaBaseMultitenant.Infrastructure.Persistence.TenantContext";
    private const string TenantJobRunner = "ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs.TenantJobRunner";
    private const string TenantSettingsLoader =
        "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers.TenantSettingsLoader";
    private const string AccountService = "ArquitecturaBaseMultitenant.Application.Services.Auth.AccountService";
    private const string ConnectService = "ArquitecturaBaseMultitenant.Application.Services.Auth.ConnectService";
    private const string ExternalLoginService = "ArquitecturaBaseMultitenant.Application.Services.Auth.ExternalLoginService";
    private const string AccountDeletionProcessingService = "ArquitecturaBaseMultitenant.Application.Services.Legal.AccountDeletionProcessingService";
    private const string InvitationService = "ArquitecturaBaseMultitenant.Application.Services.Invitations.InvitationService";
    private const string LoginCodeService = "ArquitecturaBaseMultitenant.Application.Services.Auth.LoginCodeService";
    private const string BusinessSignupService = "ArquitecturaBaseMultitenant.Application.Services.Auth.BusinessSignupService";
    private const string PlatformActionGuard = "ArquitecturaBaseMultitenant.Application.Services.Platform.PlatformActionGuard";

    // Adaptadores de Infrastructure, servicios de entrada y PlatformActionGuard, que entra después de registrar el
    // SecurityEvent (multitenancy.md). Ningún otro helper entra: InvitationService entra antes de que sus helpers
    // lean la invitación. Cada clase declarada tiene que seguir entrando: la que ya no lo necesita sale de la lista.
    private static readonly HashSet<string> Declared = CreateDeclared();
    private static HashSet<string> CreateDeclared()
    {
        var declared = new HashSet<string>(StringComparer.Ordinal)
        {
            TenantJobRunner, TenantSettingsLoader, AccountService, ConnectService, ExternalLoginService,
            AccountDeletionProcessingService, BusinessSignupService, InvitationService, LoginCodeService, PlatformActionGuard,
        };
        ContributeTenantScopeCallers(declared);
        return declared;
    }
    static partial void ContributeTenantScopeCallers(HashSet<string> declared);

    [Fact]
    public void Only_declared_adapters_and_signup_services_enter_a_tenant_scope()
    {
        var calls = new[]
        {
            Assembly.Load("ArquitecturaBaseMultitenant.Application"),
            Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"),
            Assembly.Load("ArquitecturaBaseMultitenant.Api"),
        }.SelectMany(ArchitectureIl.Calls).Where(IsEnterCall).ToArray();

        Assert.Equal(Declared.Order(StringComparer.Ordinal),
            calls.Select(call => call.Owner).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Empty(UnauthorizedCallers(calls));
    }

    [Fact]
    public void Detector_rejects_an_undeclared_scope_entry()
    {
        var control = ArchitectureIl.Calls(typeof(TenantScopeUsageTests).Assembly)
            .Where(call => call.Owner == typeof(TenantScopeUsageTests).FullName
                && call.OwnerMethod == nameof(IllegalEntry))
            .ToArray();

        Assert.Contains(control, IsEnterCall);
        Assert.NotEmpty(UnauthorizedCallers(control));
    }

    private const string TenantResolutionMiddleware = "ArquitecturaBaseMultitenant.Api.Tenancy.TenantResolutionMiddleware";
    private const string RolePermissionCache = "ArquitecturaBaseMultitenant.Infrastructure.Caching.RolePermissionCache";

    // Fijan la organización y su clase (Business o Personal) en un scope propio: la petición autenticada y los workers
    // que escriben datos compartidos (engagement), porque el guardado sella la parte y Enter deja la clase en null
    // (multitenancy.md). Los demás workers entran con Enter. Un módulo aporta los suyos en su parcial.
    private static readonly HashSet<string> AccessInitializers = CreateAccessInitializers();
    private static HashSet<string> CreateAccessInitializers()
    {
        var declared = new HashSet<string>(StringComparer.Ordinal) { TenantResolutionMiddleware, RolePermissionCache };
        ContributeAccessInitializerCallers(declared);
        return declared;
    }
    static partial void ContributeAccessInitializerCallers(HashSet<string> declared);

    [Fact]
    public void Only_declared_callers_set_the_tenant_from_an_access()
    {
        var calls = new[]
        {
            Assembly.Load("ArquitecturaBaseMultitenant.Application"),
            Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"),
            Assembly.Load("ArquitecturaBaseMultitenant.Api"),
        }.SelectMany(ArchitectureIl.Calls).Where(IsSetFromAccessCall).Where(call => call.Owner != TenantContext).ToArray();

        Assert.Equal(AccessInitializers.Order(StringComparer.Ordinal),
            calls.Select(call => call.Owner).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Detector_rejects_an_undeclared_access_initializer()
    {
        var control = ArchitectureIl.Calls(typeof(TenantScopeUsageTests).Assembly)
            .Where(call => call.Owner == typeof(TenantScopeUsageTests).FullName && call.OwnerMethod == nameof(IllegalAccess))
            .ToArray();

        Assert.Contains(control, IsSetFromAccessCall);
        Assert.DoesNotContain(control.Select(call => call.Owner), AccessInitializers.Contains);
    }

    private static bool IsSetFromAccessCall(ArchitectureIl.Call call) =>
        call.Method == nameof(ITenantAccessInitializer.SetFromAccess) && call.DeclaringType is not null
        && (call.DeclaringType == typeof(ITenantAccessInitializer).FullName || call.DeclaringType == TenantContext);

    private static void IllegalAccess(ITenantAccessInitializer initializer) =>
        initializer.SetFromAccess(Guid.Empty, ArquitecturaBaseMultitenant.Domain.Tenancy.TenantKind.Business);

    private static bool IsEnterCall(ArchitectureIl.Call call) =>
        call.Method == nameof(ITenantScope.Enter)
        && call.DeclaringType is not null
        && (call.DeclaringType == typeof(ITenantScope).FullName || call.DeclaringType == TenantContext);

    private static string[] UnauthorizedCallers(IEnumerable<ArchitectureIl.Call> calls) =>
        [.. calls.Where(IsEnterCall)
            .Where(call => !Declared.Contains(call.Owner))
            .Select(call => $"{call.Owner}.{call.OwnerMethod}")
            .Distinct(StringComparer.Ordinal)];

    private static IDisposable IllegalEntry(ITenantScope scope) => scope.Enter(Guid.Empty);
}
