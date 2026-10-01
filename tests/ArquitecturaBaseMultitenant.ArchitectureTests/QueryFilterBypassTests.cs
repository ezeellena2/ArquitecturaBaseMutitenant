using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Detecta consultas que ignoran filtros de tenant, publicación o partes fuera de las excepciones
/// autorizadas. Distingue el filtro de borrado lógico que sí puede omitirse.
/// </summary>
public sealed class QueryFilterBypassTests
{
    private const string EfExtensions = "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions";
    private const string PlatformReaders =
        "ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers.Platform";

    [Fact]
    public void Tenant_public_and_parties_filters_are_never_bypassed_outside_platform_readers()
    {
        var calls = new[]
        {
            Assembly.Load("ArquitecturaBaseMultitenant.Application"),
            Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"),
            Assembly.Load("ArquitecturaBaseMultitenant.Api"),
        }.SelectMany(ArchitectureIl.Calls).Where(IsIgnoreQueryFilters).ToArray();

        Assert.Empty(BypassViolations(calls));
    }

    [Fact]
    public void Detector_catches_unnamed_and_tenant_bypasses_but_allows_soft_delete()
    {
        var calls = ArchitectureIl.Calls(typeof(QueryFilterBypassTests).Assembly)
            .Where(call => call.Owner == typeof(QueryFilterBypassTests).FullName
                && call.OwnerMethod is nameof(BypassAll) or nameof(BypassTenant) or nameof(OnlySoftDelete))
            .Where(IsIgnoreQueryFilters)
            .ToArray();

        Assert.Equal(3, calls.Length);
        var violations = BypassViolations(calls);
        Assert.Contains(violations, value => value.EndsWith(nameof(BypassAll), StringComparison.Ordinal));
        Assert.Contains(violations, value => value.EndsWith(nameof(BypassTenant), StringComparison.Ordinal));
        Assert.DoesNotContain(violations, value => value.EndsWith(nameof(OnlySoftDelete), StringComparison.Ordinal));
    }

    private static string[] BypassViolations(IEnumerable<ArchitectureIl.Call> calls) =>
        [.. calls.Where(IsIgnoreQueryFilters)
            .Where(call => !call.Owner.StartsWith(PlatformReaders + ".", StringComparison.Ordinal))
            .Where(call => call.ParameterCount == 1 || call.ParameterCount != 2
                || !call.Literals.Contains("SoftDelete", StringComparer.Ordinal)
                || call.Literals.Any(value => value is "Tenant" or "Public" or "Parties"))
            .Select(call => $"{call.Owner}.{call.OwnerMethod}")
            .Distinct(StringComparer.Ordinal)];

    private static bool IsIgnoreQueryFilters(ArchitectureIl.Call call) =>
        call.DeclaringType == EfExtensions && call.Method == nameof(EntityFrameworkQueryableExtensions.IgnoreQueryFilters);

    private static IQueryable<object> BypassAll(IQueryable<object> rows) => rows.IgnoreQueryFilters();

    private static IQueryable<object> BypassTenant(IQueryable<object> rows) => rows.IgnoreQueryFilters(["Tenant"]);

    private static IQueryable<object> OnlySoftDelete(IQueryable<object> rows) => rows.IgnoreQueryFilters(["SoftDelete"]);
}
