using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Messaging;

/// <summary>
/// Los topes de lo que sale por correo con un código o una invitación (ADR 0063 y decisión 256), de la configuración de la
/// instalación porque dependen de la cuenta que manda. Todos cuentan las últimas 24 horas por horas, en
/// <c>identity.MessageQuotaBuckets</c>, así valen para todas las copias. Se validan al arrancar.
/// </summary>
public sealed class EmailQuotaOptions
{
    public const string SectionName = "Email:Quotas";

    /// <summary>
    /// Cuántos correos con un código o una invitación salen en 24 horas, entre todos (256: 80 mientras el Gmail sea el
    /// compartido con ArquitecturaBase). Los códigos frenan antes, en este tope menos <see cref="DailyInvitationReserve"/>.
    /// Al 80 % y al llegar queda un aviso en el log.
    /// </summary>
    [Range(1, 1_000_000, ErrorMessage = "Email:Quotas:DailyLimit must be between 1 and 1000000.")]
    public int DailyLimit { get; init; } = 80;

    /// <summary>Los lugares del tope que quedan solo para las invitaciones (256: 20). Va por debajo del tope; 0 la apaga.</summary>
    [Range(0, 1_000_000, ErrorMessage = "Email:Quotas:DailyInvitationReserve must be between 0 and 1000000.")]
    public int DailyInvitationReserve { get; init; } = 20;

    /// <summary>Los códigos por correo que se piden desde una misma dirección (una IPv6, por su /64), hayan salido o no.</summary>
    [Range(1, 1_000_000, ErrorMessage = "Email:Quotas:DailyCodeLimitPerAddress must be between 1 and 1000000.")]
    public int DailyCodeLimitPerAddress { get; init; } = 50;

    /// <summary>Los códigos por correo que una cuenta pide desde Mi cuenta.</summary>
    [Range(1, 1_000_000, ErrorMessage = "Email:Quotas:DailyCodeLimitPerAccount must be between 1 and 1000000.")]
    public int DailyCodeLimitPerAccount { get; init; } = 10;

    /// <summary>Las invitaciones por correo de una persona, sume las organizaciones que sume; también las de un operador.</summary>
    [Range(1, 1_000_000, ErrorMessage = "Email:Quotas:DailyInvitationLimitPerInviter must be between 1 and 1000000.")]
    public int DailyInvitationLimitPerInviter { get; init; } = 100;

    /// <summary>Las invitaciones por correo de una organización: sin él, una sola deja sin códigos a las demás.</summary>
    [Range(1, 1_000_000, ErrorMessage = "Email:Quotas:DailyInvitationLimitPerOrganization must be between 1 and 1000000.")]
    public int DailyInvitationLimitPerOrganization { get; init; } = 100;

    /// <summary>Hasta dónde llegan los códigos: el tope menos la reserva de las invitaciones.</summary>
    public int DailyCodeLimit => DailyLimit - DailyInvitationReserve;

    /// <summary>La reserva va por debajo del tope: si no, los códigos no saldrían nunca.</summary>
    public bool HasRoomForCodes => DailyInvitationReserve < DailyLimit;
}
