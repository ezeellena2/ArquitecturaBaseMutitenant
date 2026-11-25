using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Un canal registrado para entregar códigos; nunca envía directamente en el request.</summary>
public interface ILoginCodeChannel
{
    string Key { get; }
    LoginMethodType SupportedMethodType { get; }
    /// <summary>Método técnico para la auditoría de éxito o fallo; un canal heredado conserva Code.</summary>
    LoginAuditMethod AuditMethod => LoginAuditMethod.Code;
    bool IsAvailable { get; }
    /// <summary>Los países cuyos números admite el canal, leídos en cada pedido (270); vacío si no pide país.</summary>
    Task<IReadOnlyList<string>> CountriesAsync(CancellationToken ct);
    /// <summary>Número público E.164, nunca un identificador técnico del proveedor.</summary>
    string? ContactPhone { get; }
    /// <summary>Se usa al agregar un destino; un cambio de países no invalida métodos ya verificados.</summary>
    Task<Result> ValidateNewDestinationAsync(LoginCodeDestination destination, string? country, CancellationToken ct);

    /// <summary>El código armado para encolarlo después; async porque el correo lee el nombre de «Nombre y correos» (273).</summary>
    Task<string> RenderLoginCodeAsync(string destination, string code, int lifetimeMinutes, CultureProfile culture,
        CancellationToken ct);

    /// <summary>El código ya armado; <paramref name="lifetimeMinutes"/> es su vigencia, después de la cual no sirve mandarlo.</summary>
    void EnqueueRenderedLoginCode(string payload, int lifetimeMinutes, Guid? userId = null);

    Task EnqueueSignupAsync(string destination, string code, int lifetimeMinutes, CultureProfile culture, CancellationToken ct);
    /// <summary>El código para verificar un destino; sin cuenta en «Recuperar mi cuenta».</summary>
    Task EnqueueVerificationAsync(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture,
        CancellationToken ct);
}
