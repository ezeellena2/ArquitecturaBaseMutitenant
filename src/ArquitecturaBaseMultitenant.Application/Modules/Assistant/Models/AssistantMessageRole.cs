using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantMessageRole
{
    [JsonStringEnumMemberName("user")] User,
    [JsonStringEnumMemberName("assistant")] Assistant,
}
