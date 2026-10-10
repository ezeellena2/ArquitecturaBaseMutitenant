namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record UpdateAssistantPolicyRequest(bool? AllowOwnKey, uint? Version, string? Reason)
{
    public override string ToString() => "UpdateAssistantPolicyRequest { [REDACTED] }";
}
