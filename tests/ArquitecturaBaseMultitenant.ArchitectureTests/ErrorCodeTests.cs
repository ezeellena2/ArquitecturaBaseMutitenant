using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed partial class ErrorCodeTests
{
    // Códigos reservados del contrato HTTP y títulos de ProblemDetails.
    private static readonly string[] ReservedKeys =
    [
        "Title.Failure",
        "Title.Validation",
        "Title.Unauthorized",
        "Title.Forbidden",
        "Title.NotFound",
        "Title.Conflict",
        "Title.TooManyRequests",
        "Title.MethodNotAllowed",
        "Validation.Failed",
        "General.Unexpected",
        "General.ConcurrencyConflict",
        "Request.Invalid",
        "Request.InProgress",
        "Request.IdempotencyKeyRequired",
        "Request.IdempotencyKeyReused",
        "Http.Unauthorized",
        "Http.Forbidden",
        "Http.NotFound",
        "Http.MethodNotAllowed",
        "Http.Conflict",
        "Http.TooManyRequests",
        "Legal.AcceptanceRequired",
    ];

    [Fact]
    public void Error_codes_follow_the_area_entity_reason_format()
    {
        var malformed = ReadErrorKeys()
            .Where(key => !ReservedKeys.Contains(key, StringComparer.Ordinal))
            .Where(key => !ErrorCodeFormat().IsMatch(key));

        Assert.Empty(malformed);
    }

    [Fact]
    public void Reserved_error_keys_still_exist_in_the_resource()
    {
        Assert.Empty(ReservedKeys.Except(ReadErrorKeys(), StringComparer.Ordinal));
    }

    [Fact]
    public void Every_declared_domain_error_code_has_a_resource_key()
    {
        var resourceKeys = ReadErrorKeys().ToHashSet(StringComparer.Ordinal);
        var declaredCodes = typeof(Error).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Errors", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string)
                && field.Name.EndsWith("Code", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!);

        Assert.Empty(declaredCodes.Except(resourceKeys, StringComparer.Ordinal));
    }

    private static string[] ReadErrorKeys() =>
        XDocument.Load(Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Application", "Resources", "Errors.resx"))
            .Root!
            .Elements("data")
            .Select(data => data.Attribute("name")!.Value)
            .ToArray();

    [GeneratedRegex(@"^[A-Z][A-Za-z]+(\.[A-Z][A-Za-z0-9]+){2,}$")]
    private static partial Regex ErrorCodeFormat();
}
