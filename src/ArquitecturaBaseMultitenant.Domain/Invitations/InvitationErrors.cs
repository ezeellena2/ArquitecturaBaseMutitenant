using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Invitations;

/// <summary>Rechazos estables del flujo de invitación sin revelar tokens ni destinatarios.</summary>
public static class InvitationErrors
{
    public const string InvalidCode = "Invitations.Invitation.Invalid";
    public const string NotFoundCode = "Invitations.Invitation.NotFound";
    public const string ExpiredCode = "Invitations.Invitation.Expired";
    public const string AlreadyUsedCode = "Invitations.Invitation.AlreadyUsed";
    public const string AlreadyPendingCode = "Invitations.Invitation.AlreadyPending";
    public const string SignInRequiredCode = "Invitations.Invitation.SignInRequired";
    public const string WrongAccountCode = "Invitations.Invitation.WrongAccount";
    public const string ChannelUnavailableCode = "Invitations.Invitation.ChannelUnavailable";
    public const string PlatformOperatorCode = "Invitations.Invitation.PlatformOperator";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The invitation is not valid.");
    /// <summary>Desde una empresa, una invitación que no la incluye no existe: es un recurso ajeno.</summary>
    public static readonly Error NotFound = Error.NotFound(NotFoundCode, "The invitation was not found.");
    public static readonly Error Expired = Error.Validation(ExpiredCode, "The invitation has expired.");
    public static readonly Error AlreadyUsed = Error.Conflict(AlreadyUsedCode, "The invitation was already accepted.");
    public static readonly Error AlreadyPending = Error.Conflict(AlreadyPendingCode, "A pending invitation already exists.");
    public static readonly Error SignInRequired = Error.Unauthorized(SignInRequiredCode, "Sign in to accept the invitation.");
    public static readonly Error WrongAccount = Error.Forbidden(WrongAccountCode, "The signed-in account cannot accept this invitation.");
    public static readonly Error ChannelUnavailable = Error.Validation(ChannelUnavailableCode, "The invitation channel is unavailable.");
    /// <summary>
    /// El mismo motivo cuando el canal de Plataforma se cae por un rato (sin número conectado o sin medio de pago): «Cargar
    /// teléfonos» responde 503 y se puede volver a probar.
    /// </summary>
    public static Error ChannelUnavailableForNow() => Error.ServiceUnavailable(ChannelUnavailableCode, ChannelUnavailable.Description);
    /// <summary>Un operador de plataforma nunca entra a una organización (ADR 0042); quien invita no se entera.</summary>
    public static readonly Error PlatformOperator = Error.Forbidden(PlatformOperatorCode, "A platform operator cannot join an organization.");
}
