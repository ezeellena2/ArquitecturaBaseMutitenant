namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantInvitationsQuickDataResponse(IReadOnlyList<AssistantInvitationRowResponse> Invitations, int TotalCount, bool HasMore) : AssistantQuickDataResponse;
