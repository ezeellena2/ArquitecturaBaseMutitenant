using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantEffort
{
    [JsonStringEnumMemberName("low")] Low,
    [JsonStringEnumMemberName("medium")] Medium,
    [JsonStringEnumMemberName("high")] High,
}
