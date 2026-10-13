using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using System.Globalization;
using System.Text.Json;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;

internal sealed record AssistantToolParameter(string Name, string Type, bool Required = false, string[]? Values = null);

internal sealed class AssistantToolArguments(JsonElement json)
{
    internal int Page => Integer("page", 1);
    internal int PageSize => Integer("pageSize", AssistantTechnicalLimits.QuickRows);
    internal int Integer(string key, int fallback) => json.TryGetProperty(key, out var value) ? value.GetInt32() : fallback;
    internal string? Text(string key) => json.TryGetProperty(key, out var value) ? value.GetString()?.Trim() : null;
    internal Guid? Id(string key) => Text(key) is { } value ? Guid.Parse(value) : null;
    internal T? EnumValue<T>(string key) where T : struct, Enum => Text(key) is { } value ? Enum.Parse<T>(value) : null;
    internal DateTime? Instant(string key) => Text(key) is { } value
        ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).UtcDateTime : null;

    internal static bool IsValid(JsonElement json, IReadOnlyList<AssistantToolParameter> parameters)
    {
        if (json.ValueKind != JsonValueKind.Object) return false;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in json.EnumerateObject())
        {
            var parameter = parameters.SingleOrDefault(item => item.Name == field.Name);
            if (parameter is null || !names.Add(field.Name)) return false;
            if (parameter.Type == "integer")
            {
                if (field.Value.ValueKind != JsonValueKind.Number || !field.Value.TryGetInt32(out var number) || number < 1
                    || (field.Name == "pageSize" ? number is not (AssistantTechnicalLimits.SmallQuickPageSize or AssistantTechnicalLimits.QuickRows) : number > 1_000_000)) return false;
                continue;
            }
            if (field.Value.ValueKind != JsonValueKind.String) return false;
            var value = field.Value.GetString()!;
            if (parameter.Required && string.IsNullOrWhiteSpace(value)) return false;
            if (parameter.Values is { } options && !options.Contains(value, StringComparer.Ordinal)) return false;
            if (parameter.Type == "uuid" && (!Guid.TryParseExact(value, "D", out var id) || id == Guid.Empty)) return false;
            if (parameter.Type == "utc" && (!value.EndsWith('Z')
                || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _))) return false;
            if (parameter.Type == "month" && (value.Length != 7 || value[4] != '-' || value.Where((_, i) => i != 4).Any(c => !char.IsAsciiDigit(c))
                || !DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))) return false;
            if (value.Length > (field.Name == "cursor" ? 2048 : 100)) return false;
        }
        if (parameters.Any(parameter => parameter.Required && !names.Contains(parameter.Name))) return false;
        var arguments = new AssistantToolArguments(json);
        return arguments.Instant("fromUtc") is not { } start || arguments.Instant("toUtc") is not { } end || start < end;
    }
}
