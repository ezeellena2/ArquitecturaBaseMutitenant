using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record UpdatePlatformAssistantSettingsRequest(Guid? DefaultModelId, IReadOnlyList<Guid>? AllowedModelIds, AssistantEffort? Effort, int? DailyMessageLimit, Money? MonthlyOrganizationBudget, bool? PersonalEnabled, int? RetentionDays, AssistantKeyAction? KeyAction, string? ApiKey, uint? Version)
{
    public override string ToString() => "UpdatePlatformAssistantSettingsRequest { [REDACTED] }";
}
