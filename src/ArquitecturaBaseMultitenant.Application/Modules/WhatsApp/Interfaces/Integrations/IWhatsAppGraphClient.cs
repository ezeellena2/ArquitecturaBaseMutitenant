using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>
/// La Graph API de Meta para administrar números. Se llama fuera de la transacción del caso de uso; los errores salen como
/// <c>WhatsApp.Number.SignupExpired</c> o <c>WhatsApp.Number.MetaRejected</c>, sin el texto de Meta.
/// </summary>
public interface IWhatsAppGraphClient
{
    /// <summary>
    /// Canjea el código del alta (vive 30 segundos), suscribe la app, registra el número salvo en coexistencia y lee sus datos.
    /// En coexistencia el alta puede traer solo la cuenta: el número se resuelve desde ella.
    /// </summary>
    Task<Result<WhatsAppSignupResult>> CompleteSignupAsync(WhatsAppSignupInput input, CancellationToken ct);

    /// <summary>Si la cuenta de Meta tiene medio de pago (<c>primary_funding_id</c>).</summary>
    Task<Result<bool>> HasPaymentAsync(string wabaId, string token, CancellationToken ct);
}

public sealed record WhatsAppSignupInput(string Code, string? PhoneNumberId, string WabaId, bool Coexistence)
{
    public override string ToString() => nameof(WhatsAppSignupInput);
}

/// <summary>El token del número, el PIN de registro (null en coexistencia) y lo que Meta dice del número.</summary>
public sealed record WhatsAppSignupResult(string PhoneNumberId, string Token, string? Pin, PhoneNumber DisplayNumber, string? VerifiedName, bool HasPayment)
{
    public override string ToString() => nameof(WhatsAppSignupResult);
}
