using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantLinkStatus
{
    [JsonStringEnumMemberName("Active")] Active,
    [JsonStringEnumMemberName("Pending")] Pending,
    [JsonStringEnumMemberName("Expired")] Expired,
}
