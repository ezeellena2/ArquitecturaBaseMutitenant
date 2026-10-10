namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantPolicyResponse(bool AllowOwnKey, uint Version);
