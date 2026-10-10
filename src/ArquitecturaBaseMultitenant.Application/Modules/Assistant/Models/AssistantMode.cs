using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantMode
{
    [JsonStringEnumMemberName("PlatformKey")] PlatformKey,
    [JsonStringEnumMemberName("OwnKey")] OwnKey,
}
