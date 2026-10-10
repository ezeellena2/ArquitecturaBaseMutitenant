using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record AssistantOrganizationSettingsRow(AssistantMode Mode, Guid ModelId, AssistantEffort Effort,
    int DailyMessageLimit, Money? MonthlyBudget, int RetentionDays, bool HasKey, string? MaskedKeySuffix, uint Version);
