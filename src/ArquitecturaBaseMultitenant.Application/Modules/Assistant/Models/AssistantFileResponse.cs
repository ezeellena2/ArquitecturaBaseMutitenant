namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantFileResponse(Stream Content, string MediaType, string FileName)
{
    public override string ToString() => "AssistantFileResponse { [REDACTED] }";
}
