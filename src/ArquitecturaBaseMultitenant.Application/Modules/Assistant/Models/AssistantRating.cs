using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantRating
{
    [JsonStringEnumMemberName("up")] Up,
    [JsonStringEnumMemberName("down")] Down,
}
