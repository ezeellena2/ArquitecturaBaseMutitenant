using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record PlatformAssistantSettingsResponse(Guid DefaultModelId, IReadOnlyList<Guid> AllowedModelIds, AssistantEffort Effort, int DailyMessageLimit, Money MonthlyOrganizationBudget, bool PersonalEnabled, int RetentionDays, bool HasKey, string? MaskedKeySuffix, uint Version);
