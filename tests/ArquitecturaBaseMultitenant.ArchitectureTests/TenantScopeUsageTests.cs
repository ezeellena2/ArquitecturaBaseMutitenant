using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class TenantScopeUsageTests
{
    private const string TenantContext = "ArquitecturaBaseMultitenant.Infrastructure.Persistence.TenantContext";
    private const string TenantJobRunner = "ArquitecturaBaseMultitenant.Infrastructure.BackgroundJobs.TenantJobRunner";
    private const string TenantSettingsLoader =
        "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers.TenantSettingsLoader";

    [Fact]
    public void Only_declared_infrastructure_adapters_enter_a_technical_tenant_scope()
    {
        var calls = new[]
        {
            Assembly.Load("ArquitecturaBaseMultitenant.Application"),
            Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"),
            Assembly.Load("ArquitecturaBaseMultitenant.Api"),
        }.SelectMany(ArchitectureIl.Calls).Where(IsEnterCall).ToArray();

        Assert.Contains(calls, call => call.Owner == TenantJobRunner);
        Assert.Contains(calls, call => call.Owner == TenantSettingsLoader);
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
            .Where(call => call.Owner is not (TenantJobRunner or TenantSettingsLoader))
            .Select(call => $"{call.Owner}.{call.OwnerMethod}")
            .Distinct(StringComparer.Ordinal)];

    private static IDisposable IllegalEntry(ITenantScope scope) => scope.Enter(Guid.Empty);
}
