namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record AssistantUploadHttpRequest(IFormFile? File, uint? ConversationVersion)
{
    public override string ToString() => "AssistantUploadHttpRequest { [REDACTED] }";
}
