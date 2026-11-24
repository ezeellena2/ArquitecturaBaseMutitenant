using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// ADR 0062: desde cuándo es viejo cada dato del ingreso, a la hora de la corrida. Lo anterior a cada fecha se borra; las
/// fechas son exclusivas. Los plazos de los ingresos, los códigos y las invitaciones son los de
/// <see cref="IdentityRetentionPeriods"/>; el de las sesiones de OpenIddict queda en el código (enmienda del 2026-11-23).
/// </summary>
internal static class IdentityRetentionPolicy
{
    /// <summary>Los tokens y las autorizaciones de OpenIddict se podan a estos días de vencidos o revocados.</summary>
    internal const int SessionDaysAfterExpiry = 14;

    /// <summary>Cuántas filas borra un lote, cada uno en su transacción corta.</summary>
    internal const int BatchSize = 500;

    public static DateTime LoginCodesBeforeUtc(DateTime nowUtc, IdentityRetentionPeriods periods) =>
        Utc(nowUtc).AddDays(-Periods(periods).LoginCodeDays);

    /// <summary>Los meses son de calendario, como los decidió el usuario: «12 meses», no 360 días.</summary>
    public static DateTime LoginAuditsBeforeUtc(DateTime nowUtc, IdentityRetentionPeriods periods) =>
        Utc(nowUtc).AddMonths(-Periods(periods).LoginAuditMonths);

    public static DateTime InvitationsClosedBeforeUtc(DateTime nowUtc, IdentityRetentionPeriods periods) =>
        Utc(nowUtc).AddDays(-Periods(periods).InvitationDays);

    public static DateTime SessionsExpiredBeforeUtc(DateTime nowUtc) => Utc(nowUtc).AddDays(-SessionDaysAfterExpiry);

    private static IdentityRetentionPeriods Periods(IdentityRetentionPeriods periods)
    {
        ArgumentNullException.ThrowIfNull(periods);
        return periods;
    }

    private static DateTime Utc(DateTime nowUtc) => nowUtc.Kind == DateTimeKind.Utc
        ? nowUtc
        : throw new ArgumentException("The retention runs on UTC time.", nameof(nowUtc));
}
