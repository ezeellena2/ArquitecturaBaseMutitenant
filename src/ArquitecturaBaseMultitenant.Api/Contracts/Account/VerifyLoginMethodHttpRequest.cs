using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>
/// Recibe el código usado para verificar un método de ingreso pendiente. Conserva el texto original para
/// validarlo y oculta su valor en ToString.
/// </summary>
public sealed record VerifyLoginMethodHttpRequest([property: RawText] string? Code)
{
    public override string ToString() => nameof(VerifyLoginMethodHttpRequest);
}
