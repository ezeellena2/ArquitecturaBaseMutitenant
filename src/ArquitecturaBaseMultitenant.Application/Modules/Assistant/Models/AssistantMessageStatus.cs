using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantMessageStatus
{
    [JsonStringEnumMemberName("Complete")] Complete,
    [JsonStringEnumMemberName("Stopped")] Stopped,
    [JsonStringEnumMemberName("Failed")] Failed,
    [JsonStringEnumMemberName("Generating")] Generating,
}
