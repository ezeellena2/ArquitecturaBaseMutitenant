namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantUploadRequest(string? FileName, string? MediaType, long? ByteLength, Stream? Content, uint? ConversationVersion)
{
    public override string ToString() => "AssistantUploadRequest { [REDACTED] }";
}
