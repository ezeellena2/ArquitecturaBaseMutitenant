using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Selección efectiva para una llamada. El turno conserva un snapshot; se revalida para admitir cada llamada.</summary>
public sealed record AssistantRuntimeConfiguration(AssistantMode Mode, AssistantModelPriceResponse Model,
    AssistantProviderReference Reference, AssistantEffort Effort, int DailyMessageLimit, Money? MonthlyBudget, int RetentionDays,
    Domain.Assistant.AssistantPriceSnapshot? TitlePrice = null);
