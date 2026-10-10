using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record UpdateOrganizationAssistantSettingsRequest(AssistantMode? Mode, Guid? ModelId, AssistantEffort? Effort, int? DailyMessageLimit, Money? MonthlyBudget, AssistantKeyAction? KeyAction, string? ApiKey, uint? Version)
{
    public override string ToString() => "UpdateOrganizationAssistantSettingsRequest { [REDACTED] }";
}
