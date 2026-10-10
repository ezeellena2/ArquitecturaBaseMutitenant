using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantUsageRowKind
{
    [JsonStringEnumMemberName("organization")] Organization,
    [JsonStringEnumMemberName("person")] Person,
    [JsonStringEnumMemberName("day")] Day,
    [JsonStringEnumMemberName("personal")] Personal,
    [JsonStringEnumMemberName("operator")] Operator,
}
