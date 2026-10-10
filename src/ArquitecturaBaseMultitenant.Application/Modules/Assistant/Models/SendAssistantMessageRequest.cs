namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record SendAssistantMessageRequest(string? Text, IReadOnlyList<Guid>? AttachmentIds, uint? ConversationVersion)
{
    public override string ToString() => "SendAssistantMessageRequest { [REDACTED] }";
}
