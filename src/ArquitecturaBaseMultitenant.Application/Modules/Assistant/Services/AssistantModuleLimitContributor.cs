using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Models.Features;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// Los topes de la IA con la clave de la plataforma (ADR 0069 §4): el cupo de mensajes del día, que la organización no elige,
/// y el presupuesto del mes, en su moneda. Sus máximos son los de la configuración de Plataforma, leídos en cada uso.
/// Con clave propia no hay tope de la plataforma: valen los de la organización.
/// </summary>
internal sealed class AssistantModuleLimitContributor(IAssistantSettingsReader settings) : IModuleLimitContributor
{
    internal const string DailyMessagesKey = "assistant.dailyMessages";
    internal const string MonthlyBudgetKey = "assistant.monthlyBudget";

    public IReadOnlyList<ModuleLimitDefinition> Limits { get; } =
    [
        new(DailyMessagesKey, ModuleLimitKind.Count, null, 1),
        new(MonthlyBudgetKey, ModuleLimitKind.Amount, null, null),
    ];

    public async Task<ModuleLimitValue> ReadPlatformMaximumAsync(string key, CancellationToken cancellationToken)
    {
        if (key is not (DailyMessagesKey or MonthlyBudgetKey))
            throw new ArgumentException($"Module limit key '{key}' is not declared by the assistant.", nameof(key));
        var platform = await settings.FindPlatformAsync(cancellationToken);
        return key == DailyMessagesKey
            ? new ModuleLimitValue(platform.DailyMessageLimit, null)
            : new ModuleLimitValue(null, platform.MonthlyOrganizationBudget);
    }
}
