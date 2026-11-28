using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Configuration;

/// <summary>
/// Los topes diarios de las invitaciones por WhatsApp de «Cargar teléfonos» (decisión 1 de los arreglos de la revisión),
/// de la configuración de la instalación porque salen desde el número de Plataforma, el mismo de los códigos de ingreso.
/// Cuentan las últimas 24 horas por horas en <c>identity.MessageQuotaBuckets</c>, así valen para todas las copias. Se
/// validan al arrancar.
/// </summary>
public sealed class WhatsAppQuotaOptions
{
    public const string SectionName = "WhatsApp:Quotas";

    /// <summary>
    /// Cuántas invitaciones por WhatsApp salen en 24 horas desde el número de Plataforma, entre todas las organizaciones,
    /// por debajo del nivel de mensajería del número para dejar lugar a los códigos de ingreso. Al 80 % y al llegar queda
    /// un aviso en el log.
    /// </summary>
    [Range(1, 1_000_000, ErrorMessage = "WhatsApp:Quotas:DailyInvitationLimit must be between 1 and 1000000.")]
    public int DailyInvitationLimit { get; init; } = 200;

    /// <summary>Cuántas invitaciones por WhatsApp puede mandar una organización en 24 horas.</summary>
    [Range(1, 1_000_000, ErrorMessage = "WhatsApp:Quotas:DailyInvitationLimitPerOrganization must be between 1 and 1000000.")]
    public int DailyInvitationLimitPerOrganization { get; init; } = 100;
}
