using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>
/// Recibe la prueba que permite cancelar una baja pendiente y devuelve el retorno del ingreso. El ticket se
/// conserva sin normalizar y se oculta en ToString.
/// </summary>
public sealed record CancelAccountDeletionHttpRequest([property: RawText] string? CancelTicket)
{
    public override string ToString() => nameof(CancelAccountDeletionHttpRequest);
}

/// <summary>Indica al navegador dónde continuar después de cancelar la baja y completar el ingreso.</summary>
public sealed record CancelAccountDeletionHttpResponse(string ReturnUrl);
