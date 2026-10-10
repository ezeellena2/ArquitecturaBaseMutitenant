using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba el formato, la unicidad y las traducciones de los códigos de error. Protege el contrato
/// estable que comparten Domain, API y front.
/// </summary>
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
        "Title.ServiceUnavailable",
        "Title.NotImplemented",
        "Title.PayloadTooLarge",
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
        "Http.ServiceUnavailable",
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
        var declaredCodes = DeclaredCodes(DomainErrorTypes());

        Assert.Empty(declaredCodes.Except(resourceKeys, StringComparer.Ordinal));
    }

    [Fact]
    public void Static_domain_error_fields_have_well_formed_unique_codes()
    {
        var fields = ErrorFieldCodes(DomainErrorTypes()).ToArray();

        Assert.Empty(MalformedFieldCodes(fields));
        Assert.Empty(DuplicateFieldCodes(fields));
    }

    [Fact]
    public void Module_error_inventory_requires_English_translations_and_detects_a_missing_key()
    {
        var root = Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Application", "Modules");
        foreach (var neutral in ModuleResources(root))
        {
            var english = Path.ChangeExtension(neutral, "en.resx");
            var keys = XDocument.Load(neutral).Root!.Elements("data").Select(value => value.Attribute("name")!.Value);
            var translated = XDocument.Load(english).Root!.Elements("data").Select(value => value.Attribute("name")!.Value);
            Assert.Empty(MissingModuleErrorTranslations(keys, translated));
        }
        Assert.Equal(["Probe.Module.Missing"], MissingModuleErrorTranslations(["Probe.Module.Missing"], []));
    }
    private static string[] MissingModuleErrorTranslations(IEnumerable<string> neutral, IEnumerable<string> english) =>
        neutral.Where(value => ErrorCodeFormat().IsMatch(value)).Except(english, StringComparer.Ordinal).ToArray();

    [Fact]
    public void Probe_static_error_field_is_not_ignored()
    {
        Assert.Contains("Probe.Entity.Invalid", DeclaredCodes([typeof(ProbeErrors)]));
    }

    [Fact]
    public void Probe_exposes_malformed_and_duplicate_static_error_codes()
    {
        var fields = ErrorFieldCodes([typeof(ProbeErrors)]).ToArray();

        Assert.Equal(["ProbeErrors.Malformed: Malformed"], MalformedFieldCodes(fields));
        Assert.Equal(["Probe.Entity.Invalid: ProbeErrors.Invalid, ProbeErrors.Duplicate"], DuplicateFieldCodes(fields));
    }

    private static IEnumerable<Type> DomainErrorTypes() =>
        typeof(Error).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Errors", StringComparison.Ordinal));

    private static IEnumerable<string> DeclaredCodes(IEnumerable<Type> types) =>
        ConstCodeFields(types)
            .Concat(ErrorFieldCodes(types).Select(field => field.Code));

    private static IEnumerable<string> ConstCodeFields(IEnumerable<Type> types) =>
        types
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string)
                && field.Name.EndsWith("Code", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!);

    private static IEnumerable<(string Field, string Code)> ErrorFieldCodes(IEnumerable<Type> types) =>
        types
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(field => field.FieldType == typeof(Error))
            .Select(field => ($"{field.DeclaringType!.Name}.{field.Name}",
                ((Error?)field.GetValue(null) ?? throw new InvalidOperationException($"{field.Name} is null.")).Code));

    private static string[] MalformedFieldCodes(IEnumerable<(string Field, string Code)> fields) =>
        fields.Where(field => !ReservedKeys.Contains(field.Code, StringComparer.Ordinal) && !ErrorCodeFormat().IsMatch(field.Code))
            .Select(field => $"{field.Field}: {field.Code}")
            .ToArray();

    private static string[] DuplicateFieldCodes(IEnumerable<(string Field, string Code)> fields) =>
        fields.GroupBy(field => field.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(field => field.Field))}")
            .ToArray();

    private static class ProbeErrors
    {
        public static readonly Error Invalid = Error.Validation("Probe.Entity.Invalid", "Probe error.");
        public static readonly Error Duplicate = Error.Validation("Probe.Entity.Invalid", "Duplicate error.");
        public static readonly Error Malformed = Error.Validation("Malformed", "Malformed code.");
    }

    private static string[] ReadErrorKeys() =>
        ReadCoreErrorKeys().Concat(ModuleResources(Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Application", "Modules"))
            .SelectMany(path => XDocument.Load(path).Root!.Elements("data").Select(data => data.Attribute("name")!.Value))
            .Where(key => ErrorCodeFormat().IsMatch(key))).Distinct(StringComparer.Ordinal).ToArray();

    private static IEnumerable<string> ModuleResources(string root) => Directory.Exists(root)
        ? Directory.EnumerateFiles(root, "*.resx", SearchOption.AllDirectories).Where(path => !path.EndsWith(".en.resx", StringComparison.Ordinal)) : [];
    [Fact]
    public void Missing_last_module_directory_keeps_core_inventory_valid()
    {
        Assert.Empty(ModuleResources(Path.Combine(SolutionRoot.FullPath, "absent-module-fixture")));
        Assert.NotEmpty(ReadCoreErrorKeys());
    }

    private static string[] ReadCoreErrorKeys() =>
        XDocument.Load(Path.Combine(SolutionRoot.FullPath, "src", "ArquitecturaBaseMultitenant.Application", "Resources", "Errors.resx"))
            .Root!
            .Elements("data")
            .Select(data => data.Attribute("name")!.Value)
            .ToArray();

    [GeneratedRegex(@"^[A-Z][A-Za-z]+(\.[A-Z][A-Za-z0-9]+){2,}$")]
    private static partial Regex ErrorCodeFormat();
}
