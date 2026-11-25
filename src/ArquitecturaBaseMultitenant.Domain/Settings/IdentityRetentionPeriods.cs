namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// ADR 0062: cuánto se guardan los datos del ingreso y las invitaciones cerradas. Son los plazos que decidió el usuario
/// (pregunta 17 del traspaso 15). La decisión 272 los pasa a la configuración de Plataforma, en «Privacidad y retención»,
/// con estos valores por defecto; sin configuración, la retención usa <see cref="Default"/>.
/// </summary>
/// <param name="LoginAuditMonths">Los ingresos se borran a estos meses de calendario, salvo el último de cada cuenta.</param>
/// <param name="LoginCodeDays">Los códigos de ingreso se borran a estos días de creados.</param>
/// <param name="InvitationDays">Las invitaciones se borran a estos días de cerradas, salvo la última.</param>
public sealed record IdentityRetentionPeriods(int LoginAuditMonths, int LoginCodeDays, int InvitationDays)
{
    public static IdentityRetentionPeriods Default { get; } = new(LoginAuditMonths: 12, LoginCodeDays: 30, InvitationDays: 30);
}
