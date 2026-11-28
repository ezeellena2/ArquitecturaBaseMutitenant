namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>
/// Por qué no salió un mensaje (ADR 0066 §3): el código estable que guarda <c>FailureReason</c>, con su texto en es y en
/// (<c>Send.Failure.*</c> de WhatsAppTexts). No son respuestas HTTP. Reemplazan a <c>RecipientNotAllowed</c>,
/// <c>WindowExpired</c>, <c>Credentials</c> y <c>Permissions</c>; la migración de la B7 pasa los guardados.
/// </summary>
public static class WhatsAppSendFailures
{
    public const string NotAllowed = "WhatsApp.Send.NotAllowed";
    public const string WindowClosed = "WhatsApp.Send.WindowClosed";
    public const string Undeliverable = "WhatsApp.Send.Undeliverable";
    public const string RateLimited = "WhatsApp.Send.RateLimited";
    public const string TokenInvalid = "WhatsApp.Send.TokenInvalid";
    public const string PermissionMissing = "WhatsApp.Send.PermissionMissing";
    public const string Unavailable = "WhatsApp.Send.Unavailable";
    public const string Rejected = "WhatsApp.Send.Rejected";
    public const string ReadOnly = "WhatsApp.Send.ReadOnly";
    public const string PaymentRequired = "WhatsApp.Send.PaymentRequired";

    /// <summary>Los ocho motivos que da el transporte, más los dos que ya se responden al enviar.</summary>
    public static IReadOnlyList<string> All { get; } =
        [NotAllowed, WindowClosed, Undeliverable, RateLimited, TokenInvalid, PermissionMissing, Unavailable, Rejected, ReadOnly, PaymentRequired];

    /// <summary>
    /// Traduce un código de error de Meta a su motivo estable (<see cref="WhatsAppSendFailures"/>). Lo comparten las
    /// respuestas HTTP del cliente y los webhooks <c>status: failed</c>. Sin código conocido devuelve <c>null</c>.
    /// </summary>
    public static string? ForMetaError(int? code, bool serverError = false) => code switch
    {
        131030 => NotAllowed,
        131047 => WindowClosed,
        131026 => Undeliverable,
        131042 => PaymentRequired,
        131049 or 4 or 80007 or 130429 or 131048 or 131056 => RateLimited,
        131000 or 131016 or 2 or 133004 when serverError => Unavailable,
        0 or 190 => TokenInvalid,
        3 or 10 or (>= 200 and <= 299) => PermissionMissing,
        _ => null,
    };

    /// <summary>Motivo de un <c>status: failed</c> que llega por webhook (sin estado HTTP).</summary>
    public static string ForWebhookError(int? code) => ForMetaError(code, serverError: true) ?? Rejected;
}
