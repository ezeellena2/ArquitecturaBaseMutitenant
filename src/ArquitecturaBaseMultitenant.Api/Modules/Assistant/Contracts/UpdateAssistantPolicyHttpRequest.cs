
namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record UpdateAssistantPolicyHttpRequest(bool? AllowOwnKey, uint? Version, string? Reason)
{
    public override string ToString() => "UpdateAssistantPolicyHttpRequest { [REDACTED] }";
}
