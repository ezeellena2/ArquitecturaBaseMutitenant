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

    // These converters serialize invariant wire values, not text shown to a person.
    private static readonly HashSet<string> WireFormatters = new(StringComparer.Ordinal)
    {
        "ArquitecturaBaseMultitenant.Api.Json.DateOnlyConverter",
        "ArquitecturaBaseMultitenant.Api.Json.TimeOnlyConverter",
        "ArquitecturaBaseMultitenant.Api.Json.UtcDateTimeConverter",
        "ArquitecturaBaseMultitenant.Api.ErrorHandling.RetryAfterHeaderFormatter",
    };

    [Fact]
    public void Production_code_uses_display_formatter_for_number_and_date_presentation()
    {
        var offenders = ProductionAssemblies
            .Select(Assembly.Load)
            .SelectMany(ManualFormattingCalls)
            .Where(call => call.Owner != FormatterType
                && !call.Owner.StartsWith(FormatterType + "/", StringComparison.Ordinal)
                && !WireFormatters.Contains(call.Owner));

        var violations = offenders.Select(call => $"{call.Owner}.{call.SourceMethod}: {call.Target}").ToArray();
        Assert.True(violations.Length == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Detector_finds_number_and_date_formatting_but_not_guid_serialization()
    {
        var calls = ManualFormattingCalls(Assembly.GetExecutingAssembly())
            .Where(call => call.Owner.Contains(nameof(ManualFormatProbe), StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(calls, call => call.Target.Contains("System.Decimal", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("System.DateTime", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("DefaultInterpolatedStringHandler::AppendFormatted", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("System.String::Format", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("System.Text.StringBuilder::AppendFormat", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("System.Decimal::ToString()", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("System.Decimal::ToString(System.IFormatProvider)", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("System.DateTime::ToString()", StringComparison.Ordinal));
        Assert.True(calls.Any(call => call.Target.Contains("System.Half::ToString()", StringComparison.Ordinal)),
            string.Join(Environment.NewLine, calls.Select(call => call.Target)));
        Assert.Contains(calls, call => call.Target.Contains("System.Int128::ToString()", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("System.UInt128::ToString()", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("AppendFormatted<System.IFormattable>", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("AppendFormatted<System.Object>", StringComparison.Ordinal));
        Assert.Contains(calls, call => call.Target.Contains("System.Decimal::ToString(System.String,System.IFormatProvider)", StringComparison.Ordinal)
            && call.Owner.Contains("<>c", StringComparison.Ordinal));
        Assert.DoesNotContain(calls, call => call.Target.Contains("System.Guid", StringComparison.Ordinal));
        Assert.DoesNotContain(calls, call => call.SourceMethod == nameof(ManualFormatProbe.InterpolatedBoxedIdentifier));
    }

    private static IEnumerable<FormattingCall> ManualFormattingCalls(Assembly assembly)
    {
        using var module = ModuleDefinition.ReadModule(assembly.Location);
        return
        [
            .. module.GetTypes()
                .Where(type => !IsGenerated(type)
                    && !type.Name.StartsWith("<>f__AnonymousType", StringComparison.Ordinal))
                .SelectMany(type => type.Methods.Where(method => method.HasBody
                        && !(method.Name == "PrintMembers"
                            && method.CustomAttributes.Any(attribute =>
                                attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")))
                    .SelectMany(method => method.Body.Instructions
                        .Where(instruction => instruction.OpCode.Code is Code.Call or Code.Callvirt)
                        .Where(instruction => instruction.Operand is MethodReference call
                            && IsManualFormatting(instruction, call))
                        .Select(instruction => new FormattingCall(type.FullName, method.Name,
                            FormattingTarget(instruction, (MethodReference)instruction.Operand))))),
        ];
    }

    private static bool IsManualFormatting(Instruction instruction, MethodReference method)
    {
        var owner = method.DeclaringType.FullName;
        if (owner == "System.Object" && method.Name == "ToString"
            && instruction.Previous is { OpCode.Code: Code.Constrained, Operand: TypeReference constrainedType })
        {
            return IsNumberOrDate(constrainedType);
        }

        if (owner == "System.Runtime.CompilerServices.DefaultInterpolatedStringHandler"
            && method.Name == "AppendFormatted"
            && method.Parameters.Count > 1
            && method.Parameters.Skip(1).Any(parameter => parameter.ParameterType.FullName == "System.String")
            && method is GenericInstanceMethod { GenericArguments.Count: 1 } formatted)
        {
            var valueType = formatted.GenericArguments[0];
            return IsNumberOrDate(valueType)
                || (valueType.FullName is "System.Object" or "System.IFormattable"
                    && IsBoxedNumberOrDate(instruction));
        }

        if ((owner == "System.String" && method.Name == "Format")
            || (owner == "System.Text.StringBuilder" && method.Name == "AppendFormat"))
        {
            return true;
        }

        if (owner is "System.DateTime" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly")
        {
            return method.Name is "ToShortDateString" or "ToLongDateString" or "ToShortTimeString" or "ToLongTimeString"
                || method.Name == "ToString";
        }

        return IsNumberOrDate(method.DeclaringType) && method.Name == "ToString";
    }

    private static string FormattingTarget(Instruction instruction, MethodReference method) =>
        instruction.Previous is { OpCode.Code: Code.Constrained, Operand: TypeReference constrainedType }
            ? $"{constrainedType.FullName}::ToString() [{method.FullName}]"
            : method.FullName;

    private static bool IsBoxedNumberOrDate(Instruction instruction)
    {
        // The optional format string (and alignment) sit between the value and the call.
        var previous = instruction.Previous;
        for (var depth = 0; previous is not null && depth < 3; depth++, previous = previous.Previous)
        {
            if (previous.OpCode.Code == Code.Box && previous.Operand is TypeReference boxedType)
            {
                return IsNumberOrDate(boxedType);
            }
        }

        return false;
    }

    private static bool IsNumberOrDate(TypeReference type)
    {
        if (type is GenericInstanceType nullable && nullable.ElementType.FullName == "System.Nullable`1")
        {
            return IsNumberOrDate(nullable.GenericArguments[0]);
        }

        return type.FullName is "System.DateTime" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly"
            or "System.Decimal" or "System.Double" or "System.Single" or "System.Half"
            or "System.Int16" or "System.Int32" or "System.Int64"
            or "System.UInt16" or "System.UInt32" or "System.UInt64"
            or "System.Byte" or "System.SByte" or "System.Int128" or "System.UInt128";
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

    private sealed record FormattingCall(string Owner, string SourceMethod, string Target);

    private static class ManualFormatProbe
    {
        public static string Number(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

        public static string Date(DateTime date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        public static string Interpolated(decimal amount) => $"{amount:N2}";

        public static string InterpolatedIdentifier(Guid id) => $"{id:N}";

        public static string InterpolatedBoxedIdentifier(Guid id) => $"{(object)id:N}";

        public static string InterpolatedAsFormattable(decimal amount) => $"{(IFormattable)amount:N2}";

        public static string InterpolatedAsObject(decimal amount) => $"{(object)amount:N2}";

        public static string Composite(decimal amount) =>
            string.Format(CultureInfo.InvariantCulture, "{0:N2}", amount);

        public static string Builder(decimal amount) =>
            new System.Text.StringBuilder().AppendFormat(CultureInfo.InvariantCulture, "{0:N2}", amount).ToString();

        public static string[] Lambda(decimal[] values) =>
            values.Select(value => value.ToString("N2", CultureInfo.InvariantCulture)).ToArray();

#pragma warning disable CA1305 // This probe must compile an intentionally culture-dependent call.
        public static string NumberWithoutFormat(decimal amount) => amount.ToString();

        public static string NumberWithProvider(decimal amount) => amount.ToString(CultureInfo.InvariantCulture);

        public static string DateWithoutFormat(DateTime date) => date.ToString();

        public static string HalfWithoutFormat(Half value) => value.ToString();

        public static string Int128WithoutFormat(Int128 value) => value.ToString();

        public static string UInt128WithoutFormat(UInt128 value) => value.ToString();
#pragma warning restore CA1305

        public static string Identifier(Guid id) => id.ToString("N");
    }
}
