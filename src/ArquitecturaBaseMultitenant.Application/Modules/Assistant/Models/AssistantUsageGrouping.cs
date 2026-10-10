using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantUsageGrouping
{
    [JsonStringEnumMemberName("day")] Day,
    [JsonStringEnumMemberName("person")] Person,
}
