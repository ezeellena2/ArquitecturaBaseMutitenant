namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record UpdateAssistantConversationRequest(string? Title, bool? IsSaved, uint? Version)
{
    public override string ToString() => "UpdateAssistantConversationRequest { [REDACTED] }";
}
