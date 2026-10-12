namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantAttachmentRow(Guid Id, Guid ConversationId, Guid? MessageId, string FileName, string MediaType, string StorageKey, long ByteLength, uint Version)
{
    public override string ToString() => "AssistantAttachmentRow { [REDACTED] }";
}
