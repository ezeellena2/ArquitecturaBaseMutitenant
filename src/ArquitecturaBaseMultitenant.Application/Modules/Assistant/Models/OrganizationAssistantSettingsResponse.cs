using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record OrganizationAssistantSettingsResponse(AssistantMode Mode, Guid ModelId, AssistantEffort Effort, int DailyMessageLimit, Money? MonthlyBudget, bool AllowOwnKey, bool HasAlertRecipient, int RetentionDays, bool HasKey, string? MaskedKeySuffix, uint Version);
