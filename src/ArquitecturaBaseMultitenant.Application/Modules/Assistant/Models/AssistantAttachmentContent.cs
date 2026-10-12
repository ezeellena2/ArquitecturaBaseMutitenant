namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantAttachmentContent(Guid Id, string MediaType, byte[] Bytes)
{
    public override string ToString() => "AssistantAttachmentContent { [REDACTED] }";
}
