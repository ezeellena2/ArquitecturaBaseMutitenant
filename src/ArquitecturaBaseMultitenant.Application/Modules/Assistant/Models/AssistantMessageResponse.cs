namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Un mensaje del historial y del SSE; <c>Proposal</c> es la propuesta del turno, si la hay (ADR 0068 §8).</summary>
public sealed record AssistantMessageResponse(Guid Id, AssistantMessageRole Role, string Text, AssistantMessageStatus Status, DateTime CreatedAtUtc, uint Version, IReadOnlyList<AssistantAttachmentSummaryResponse> Attachments, IReadOnlyList<AssistantLinkResponse> Links, AssistantRating? Rating,
    AssistantMessageProposalResponse? Proposal = null);
