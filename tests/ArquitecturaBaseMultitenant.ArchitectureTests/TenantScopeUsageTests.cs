using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Detecta entradas a un alcance de organización fuera de los adaptadores y altas autorizados. Protege el
/// punto donde se cambia el contexto de aislamiento.
/// </summary>
public sealed class TenantScopeUsageTests
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
    private const string BusinessSignupService = "ArquitecturaBaseMultitenant.Application.Services.Auth.BusinessSignupService";
    private const string PlatformActionGuard = "ArquitecturaBaseMultitenant.Application.Services.Platform.PlatformActionGuard";

    // Adaptadores de Infrastructure, servicios de entrada y PlatformActionGuard, que entra después de registrar el
    // SecurityEvent (multitenancy.md). Ningún otro helper entra: InvitationService entra antes de que sus helpers
    // lean la invitación. Cada clase declarada tiene que seguir entrando: la que ya no lo necesita sale de la lista.
    private static readonly HashSet<string> Declared = new(StringComparer.Ordinal)
    {
        TenantJobRunner, TenantSettingsLoader, AccountService, ConnectService, ExternalLoginService,
        AccountDeletionProcessingService, BusinessSignupService, InvitationService, PlatformActionGuard,
    };

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
