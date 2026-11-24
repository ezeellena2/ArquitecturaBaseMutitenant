using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

public sealed record OrganizationAssistantSettingsResponse(AssistantMode Mode, Guid ModelId, AssistantEffort Effort, int DailyMessageLimit, Money? MonthlyBudget, bool AllowOwnKey, bool HasAlertRecipient, int RetentionDays, bool HasKey, string? MaskedKeySuffix, uint Version, IReadOnlyList<AssistantModelPriceResponse> PlatformKeyModels, IReadOnlyList<AssistantModelPriceResponse> OwnKeyModels)
{
    /// <summary>La última falla del proveedor que arregla quien ve esta configuración (ADR 0064 §7, B13); null sin falla.</summary>
    public string? ProviderIssue { get; init; }

    /// <summary>El proveedor guardado no lo conoce esta versión: la conexión queda pausada (ADR 0064 §6, B13).</summary>
    public bool UnsupportedProvider { get; init; }
}
