using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record UpdateOrganizationAssistantSettingsHttpRequest(AssistantMode? Mode, Guid? ModelId, AssistantEffort? Effort, int? DailyMessageLimit, Money? MonthlyBudget, AssistantKeyAction? KeyAction, string? ApiKey, uint? Version)
{
    public override string ToString() => "UpdateOrganizationAssistantSettingsHttpRequest { [REDACTED] }";
}
