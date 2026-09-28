using System.Globalization;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class NoManualFormattingTests
{
    private static readonly string[] ProductionAssemblies =
    [
        "ArquitecturaBaseMultitenant.Domain",
        "ArquitecturaBaseMultitenant.Application",
        "ArquitecturaBaseMultitenant.Infrastructure",
        "ArquitecturaBaseMultitenant.Api",
    ];

    private const string FormatterType =
        "ArquitecturaBaseMultitenant.Application.Common.Formatting.DisplayFormatter";

    // These converters serialize the invariant wire contract, not text shown to a person.
    private static readonly HashSet<string> WireDateConverters = new(StringComparer.Ordinal)
    {
        "ArquitecturaBaseMultitenant.Api.Json.DateOnlyConverter",
        "ArquitecturaBaseMultitenant.Api.Json.TimeOnlyConverter",
        "ArquitecturaBaseMultitenant.Api.Json.UtcDateTimeConverter",
    };

    [Fact]
    public void Production_code_uses_display_formatter_for_number_and_date_presentation()
    {
        var offenders = ProductionAssemblies
            .Select(Assembly.Load)
            .SelectMany(ManualFormattingCalls)
            .Where(call => call.Owner != FormatterType
                && !call.Owner.StartsWith(FormatterType + "/", StringComparison.Ordinal)
                && !WireDateConverters.Contains(call.Owner));

        Assert.Empty(offenders.Select(call => $"{call.Owner}: {call.Target}"));
    }

    [Fact]
    public void Detector_finds_number_and_date_formatting_but_not_guid_serialization()
    {
        var calls = ManualFormattingCalls(Assembly.GetExecutingAssembly())
            .Where(call => call.Owner.Contains(nameof(ManualFormatProbe), StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(calls, call => call.Target.Contains("System.Decimal", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("System.DateTime", StringComparison.Ordinal));
        Assert.DoesNotContain(calls, call => call.Target.Contains("System.Guid", StringComparison.Ordinal));
    }

    private static IEnumerable<FormattingCall> ManualFormattingCalls(Assembly assembly)
    {
        using var module = ModuleDefinition.ReadModule(assembly.Location);
        return
        [
            .. module.GetTypes()
                .Where(type => !IsGenerated(type))
                .SelectMany(type => type.Methods.Where(method => method.HasBody)
                    .SelectMany(method => method.Body.Instructions
                        .Where(instruction => instruction.OpCode.Code is Code.Call or Code.Callvirt)
                        .Select(instruction => instruction.Operand)
                        .OfType<MethodReference>()
                        .Where(IsManualFormatting)
                        .Select(call => new FormattingCall(type.FullName, call.FullName)))),
        ];
    }

    private static bool IsManualFormatting(MethodReference method)
    {
        var owner = method.DeclaringType.FullName;
        if (owner is "System.DateTime" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly")
        {
            return method.Name is "ToShortDateString" or "ToLongDateString" or "ToShortTimeString" or "ToLongTimeString"
                || (method.Name == "ToString" && method.Parameters.FirstOrDefault()?.ParameterType.FullName == "System.String");
        }

        if (owner is "System.Decimal" or "System.Double" or "System.Single"
            or "System.Int16" or "System.Int32" or "System.Int64"
            or "System.UInt16" or "System.UInt32" or "System.UInt64"
            or "System.Byte" or "System.SByte")
        {
            return method.Name == "ToString" && method.Parameters.FirstOrDefault()?.ParameterType.FullName == "System.String";
        }

        return false;
    }

    private static TypeDefinition Outermost(TypeDefinition type)
    {
        while (type.DeclaringType is not null)
        {
            type = type.DeclaringType;
        }

        return type;
    }

    private static bool IsGenerated(TypeDefinition type) =>
        Outermost(type).CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == "System.CodeDom.Compiler.GeneratedCodeAttribute");

    private sealed record FormattingCall(string Owner, string Target);

    private static class ManualFormatProbe
    {
        public static string Number(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

        public static string Date(DateTime date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        public static string Identifier(Guid id) => id.ToString("N");
    }
}
