using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantStateResponse(bool Configured, bool CanConfigure, IReadOnlyList<AssistantModelOptionResponse> Models, IReadOnlyList<AssistantQuickQueryResponse> QuickQueries, int DailyLimit, DateTime DailyResetAtUtc, int? RemainingMessages, Money? MonthlyBudget, Money? UsedCost, string NoticeVersion, bool NoticeAccepted, int RetentionDays, long MaxAttachmentBytes, int MaxAttachments);
