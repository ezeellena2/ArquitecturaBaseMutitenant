
namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record SendAssistantMessageHttpRequest(string? Text, IReadOnlyList<Guid>? AttachmentIds, uint? ConversationVersion)
{
    public override string ToString() => "SendAssistantMessageHttpRequest { [REDACTED] }";
}
