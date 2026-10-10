using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public enum AssistantProvider
{
    [JsonStringEnumMemberName("Anthropic")] Anthropic,
}
