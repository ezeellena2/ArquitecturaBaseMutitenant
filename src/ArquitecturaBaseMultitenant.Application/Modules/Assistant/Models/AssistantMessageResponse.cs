namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantMessageResponse(Guid Id, AssistantMessageRole Role, string Text, AssistantMessageStatus Status, DateTime CreatedAtUtc, uint Version, IReadOnlyList<AssistantAttachmentSummaryResponse> Attachments, IReadOnlyList<AssistantLinkResponse> Links, AssistantRating? Rating);
