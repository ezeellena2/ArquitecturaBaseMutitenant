using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

internal sealed record AssistantStateConfigurationRow(bool Configured, bool CanConfigure, AssistantMode Mode,
    IReadOnlyList<AssistantModelOptionResponse> Models, int DailyLimit, Money? MonthlyBudget, int RetentionDays);
