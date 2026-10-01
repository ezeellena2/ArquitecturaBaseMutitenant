using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>
/// Recibe la prueba de titularidad para quitar o elegir como principal un método de ingreso. El método
/// objetivo llega en la ruta; el ticket se conserva sin normalizar y se oculta en ToString.
/// </summary>
public sealed record ChangeLoginMethodHttpRequest([property: RawText] string? ReauthTicket)
{
    public override string ToString() => nameof(ChangeLoginMethodHttpRequest);
}
