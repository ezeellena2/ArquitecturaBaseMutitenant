using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Time;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>El día de un cupo diario de la IA: de quién es (la organización, o nadie fuera de una), cuál es y cuándo se renueva.</summary>
internal sealed record AssistantQuotaDay(Guid? OrganizationId, DateOnly Day, DateTime ResetAtUtc);

/// <summary>
/// El corte de los cupos diarios de la IA (271, que enmienda la 0064 §3): en una organización, la medianoche de su zona, con un
/// cupo por persona en cada una, también con la clave de la plataforma; fuera de una (el acceso personal y Plataforma), la de
/// la zona del perfil. El tope mensual sigue el mes de la organización, con la misma zona.
/// </summary>
internal sealed class AssistantQuotaDayResolver(IExecutionActorFactory actors, ITenantSettingsReader settings, ITimeZoneService zones)
{
    /// <summary>La zona que corta el día: la de la organización (UTC si no tiene), o la del perfil fuera de una.</summary>
    internal async Task<string> ZoneAsync(Access access, string profileTimeZoneId, CancellationToken ct) => access == Access.Business
        ? (await settings.FindCurrentAsync(ct))?.DefaultTimeZoneId ?? TimeZoneInfo.Utc.Id
        : profileTimeZoneId;

    /// <summary>El día de quien pide ahora; null sin una sesión que se pueda leer.</summary>
    internal async Task<AssistantQuotaDay?> CurrentAsync(DateTime nowUtc, CancellationToken ct)
    {
        var actor = await actors.CaptureAsync(ct);
        if (actor.IsFailure) return null;
        return Of(OrganizationOf(actor.Value.Access, actor.Value.TenantId), nowUtc,
            await ZoneAsync(actor.Value.Access, actor.Value.TimeZoneId, ct));
    }

    internal AssistantQuotaDay Of(Guid? organizationId, DateTime nowUtc, string timeZoneId)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(nowUtc));
        var day = DateOnly.FromDateTime(zones.ConvertToLocal(nowUtc, timeZoneId));
        return new(organizationId, day, zones.GetDayRangeUtc(day, timeZoneId).EndUtc);
    }

    /// <summary>De quién es el cupo con la clave de la plataforma: la organización en el acceso de empresa; nadie fuera de una.</summary>
    internal static Guid? OrganizationOf(Access access, Guid? scopeTenantId) => access == Access.Business ? scopeTenantId : null;
}
