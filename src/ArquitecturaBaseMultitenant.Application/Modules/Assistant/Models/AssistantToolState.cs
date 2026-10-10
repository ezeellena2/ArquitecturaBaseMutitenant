using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantToolState
{
    [JsonStringEnumMemberName("running")] Running,
    [JsonStringEnumMemberName("complete")] Complete,
    [JsonStringEnumMemberName("failed")] Failed,
}
