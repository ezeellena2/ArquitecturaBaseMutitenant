using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>
/// Recibe el motivo de baja y la prueba de titularidad necesaria para solicitarla. ToString oculta ambos
/// valores para evitar que se registren en logs.
/// </summary>
public sealed record RequestAccountDeletionHttpRequest(string? Reason, [property: RawText] string? ReauthTicket)
{
    public override string ToString() => nameof(RequestAccountDeletionHttpRequest);
}
