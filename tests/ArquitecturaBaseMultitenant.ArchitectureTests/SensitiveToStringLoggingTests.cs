using System.Reflection;
using ArquitecturaBaseMultitenant.Api.Contracts.Common;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que los contratos con datos sensibles oculten sus valores en ToString. Evita que el registro
/// de una solicitud revele correos, códigos o tickets.
/// </summary>
public sealed class SensitiveToStringLoggingTests
{
    private const string Sentinel = "private-value-7fbd63";
    private static readonly Assembly ApiAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Api");

    [Fact]
    public void Sensitive_http_requests_hide_values_from_ToString()
    {
        var violations = SensitiveRequests(ApiAssembly)
            .Where(type => !HasSafeToString(CreateWithSentinel(type), Sentinel))
            .Select(type => type.Name)
            .ToArray();

        Assert.True(violations.Length == 0, string.Join(", ", violations));
    }

    [Fact]
    public void Detector_distinguishes_record_default_ToString_from_redacted_override()
    {
        Assert.False(HasSafeToString(new UnsafePhoneHttpRequest(Sentinel), Sentinel));
        Assert.True(HasSafeToString(new PhoneInputHttpRequest("AR", Sentinel), Sentinel));
    }

    // Los pedidos y las partes que llevan adentro (*HttpInput), del núcleo y de los módulos (api-http.md). Un sí o no, un id o un
    // número (OfferAccountLink) no llevan el dato: cuentan los textos y las partes.
    private static IEnumerable<Type> SensitiveRequests(Assembly assembly) =>
        assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false }
            && (type.Name.EndsWith("HttpRequest", StringComparison.Ordinal) || type.Name.EndsWith("HttpInput", StringComparison.Ordinal))
            && type.Namespace is { } ns
            && (ns.StartsWith("ArquitecturaBaseMultitenant.Api.Contracts", StringComparison.Ordinal)
                || (ns.StartsWith("ArquitecturaBaseMultitenant.Api.Modules.", StringComparison.Ordinal)
                    && ns.Contains(".Contracts", StringComparison.Ordinal)))
            && type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => !property.PropertyType.IsValueType
                    && SensitiveNames.Any(name => property.Name.Contains(name, StringComparison.OrdinalIgnoreCase))));

    private static readonly string[] SensitiveNames =
        ["Email", "Phone", "Number", "Tax", "Token", "Password", "Secret", "Code", "Link", "Document"];

    private static object CreateWithSentinel(Type type)
    {
        var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(candidate => candidate.GetParameters().Length)
            .FirstOrDefault() ?? throw new InvalidOperationException($"No public constructor for {type.FullName}.");
        var arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(string)
                ? (object?)Sentinel
                : parameter.ParameterType.IsValueType
                    ? Activator.CreateInstance(parameter.ParameterType)
                    : null)
            .ToArray();
        return constructor.Invoke(arguments);
    }

    private static bool HasSafeToString(object value, string sentinel) =>
        value.ToString() is { } rendered && !rendered.Contains(sentinel, StringComparison.Ordinal);

    private sealed record UnsafePhoneHttpRequest(string? Number);
}
