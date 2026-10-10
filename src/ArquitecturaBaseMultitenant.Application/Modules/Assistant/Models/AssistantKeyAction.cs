using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantKeyAction
{
    [JsonStringEnumMemberName("keep")] Keep,
    [JsonStringEnumMemberName("replace")] Replace,
    [JsonStringEnumMemberName("clear")] Clear,
}
