
namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record UpdateAssistantConversationHttpRequest(string? Title, bool? IsSaved, uint? Version)
{
    public override string ToString() => "UpdateAssistantConversationHttpRequest { [REDACTED] }";
}
