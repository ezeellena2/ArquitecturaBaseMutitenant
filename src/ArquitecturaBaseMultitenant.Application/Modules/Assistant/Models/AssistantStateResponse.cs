using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>
/// El estado del asistente para quien pide. <c>DailyResetTimeZoneId</c> es la zona en que corta el cupo del día (271): la de la
/// organización en el acceso de empresa y la del perfil fuera de una; la pantalla dice la hora de <c>DailyResetAtUtc</c> en ella.
/// </summary>
public sealed record AssistantStateResponse(bool Configured, bool CanConfigure, IReadOnlyList<AssistantModelOptionResponse> Models, IReadOnlyList<AssistantQuickQueryResponse> QuickQueries, int DailyLimit, DateTime DailyResetAtUtc, int? RemainingMessages, Money? MonthlyBudget, Money? UsedCost, string NoticeVersion, bool NoticeAccepted, int RetentionDays, long MaxAttachmentBytes, int MaxAttachments, AssistantBudgetProgressResponse? MonthlyBudgetProgress, string DailyResetTimeZoneId);
