using System.Net;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;

/// <summary>
/// ADR 0064 §7 (B13): traduce lo que entrega <see cref="AssistantChatClient"/> (un pedido fallido con su estado HTTP o la falla de
/// su transporte) al código de la falla. Nunca por el cuerpo del proveedor, que no se lee ni se registra, y nada acá repite un
/// pedido. Copia <c>LlmProviderFailures</c> de ArquitecturaBase.
/// </summary>
internal static class AssistantProviderFailureClassifier
{
    /// <summary>
    /// Con estado, por el estado. Sin estado, lo que no salió (el nombre sin resolver, la conexión rechazada, el TLS o los 10 s de
    /// conexión) no se mandó: no disponible. Lo demás (la espera vencida, la respuesta cortada o ilegible) pudo empezar a generar:
    /// incierto.
    /// </summary>
    public static string Classify(HttpRequestException failure) => failure switch
    {
        { StatusCode: { } status } => FromStatus(status),
        { HttpRequestError: HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError }
            => AssistantErrors.ProviderUnavailableCode,
        _ => AssistantErrors.ProviderUncertainCode,
    };

    /// <summary>
    /// La clave (401, 403), el cupo o el límite de la cuenta del proveedor (402, 429) y el modelo, el archivo o el pedido (400, 404)
    /// tienen su código; el resto de los 4xx (y una redirección que no se sigue) es un rechazo, y un 5xx es incierto porque la
    /// generación pudo empezar.
    /// </summary>
    public static string FromStatus(HttpStatusCode status) => (int)status switch
    {
        401 or 403 => AssistantErrors.ProviderCredentialRejectedCode,
        402 or 429 => AssistantErrors.ProviderLimitedCode,
        400 or 404 => AssistantErrors.ProviderModelRejectedCode,
        >= 500 => AssistantErrors.ProviderUncertainCode,
        _ => AssistantErrors.ProviderRejectedCode,
    };
}
