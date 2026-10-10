namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantQuickResultResponse(string Text, IReadOnlyList<AssistantLinkResponse> Links, AssistantQuickDataResponse Data);
