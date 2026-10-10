namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantMaskedLoginMethodResponse(string Type, string MaskedValue, bool IsVerified, bool IsUsable);
