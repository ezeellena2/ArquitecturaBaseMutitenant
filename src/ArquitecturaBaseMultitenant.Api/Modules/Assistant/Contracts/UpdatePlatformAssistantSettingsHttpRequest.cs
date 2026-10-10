using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;

public sealed record UpdatePlatformAssistantSettingsHttpRequest(Guid? DefaultModelId, IReadOnlyList<Guid>? AllowedModelIds, AssistantEffort? Effort, int? DailyMessageLimit, Money? MonthlyOrganizationBudget, bool? PersonalEnabled, int? RetentionDays, AssistantKeyAction? KeyAction, string? ApiKey, uint? Version)
{
    public override string ToString() => "UpdatePlatformAssistantSettingsHttpRequest { [REDACTED] }";
}
