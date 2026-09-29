using System.Reflection;
using ArquitecturaBaseMultitenant.Api.Contracts.Common;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class SensitiveToStringLoggingTests
{
    private const string Sentinel = "private-value-7fbd63";
    private static readonly Assembly ApiAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Api");

    [Fact]
    public void Sensitive_http_requests_hide_values_from_ToString()
    {
        var violations = SensitiveRequests(ApiAssembly)
            .Where(type => !HasSafeToString(CreateWithSentinel(type), Sentinel))
            .Select(type => type.FullName);

        Assert.Empty(violations);
    }

    [Fact]
    public void Detector_distinguishes_record_default_ToString_from_redacted_override()
    {
        Assert.False(HasSafeToString(new UnsafePhoneHttpRequest(Sentinel), Sentinel));
        Assert.True(HasSafeToString(new PhoneInputHttpRequest("AR", Sentinel), Sentinel));
    }

    private static IEnumerable<Type> SensitiveRequests(Assembly assembly) =>
        assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false }
            && type.Name.EndsWith("HttpRequest", StringComparison.Ordinal)
            && type.Namespace?.StartsWith("ArquitecturaBaseMultitenant.Api.Contracts", StringComparison.Ordinal) == true
            && type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => SensitiveNames.Any(name => property.Name.Contains(name, StringComparison.OrdinalIgnoreCase))));

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
