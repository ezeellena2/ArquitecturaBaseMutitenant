using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// ADR 0064 §2: antes de leer el cuerpo de una subida se miran el cupo diario de la persona y el tope mensual de la
/// organización, con la guarda que admite un turno y la configuración que lo pagaría, sin reservar nada. Solo un tope frena
/// acá: sin configuración o sin sesión, la subida sigue y responde como siempre.
/// </summary>
internal sealed class AssistantUploadPolicy(AssistantTurnPreparationBuilder preparation, IAssistantConfigurationPolicy configuration,
    AssistantLimitGuard limits)
{
    /// <summary>Arma, fuera de la transacción, lo que admitiría un turno ahora; null si no hay nada que mirar.</summary>
    internal async Task<AssistantFinanceAdmission?> PrepareAsync(CancellationToken ct)
    {
        var prepared = await preparation.BuildAsync(null, ct);
        if (prepared.IsFailure) return null;
        var resolved = await configuration.ResolveAsync(ct);
        if (resolved.IsFailure) return null;
        var (actor, config) = (prepared.Value.Actor, resolved.Value);
        return new AssistantFinanceAdmission(Guid.Empty, new AssistantFinancialBinding(actor.OwnerUserId, actor.Access, actor.TenantId),
            config.Mode, config.DailyMessageLimit, config.MonthlyBudget, prepared.Value.OrganizationTimeZoneId,
            AssistantPriceSnapshotBuilder.Build(config.Model, config.Reference), null);
    }

    /// <summary>Dentro de la transacción del servicio: el 429 de siempre si un turno no entraría.</summary>
    internal Task<Result> CheckAsync(AssistantFinanceAdmission admission, CancellationToken ct) => limits.CheckAsync(admission, ct);
}
