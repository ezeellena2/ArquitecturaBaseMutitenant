namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>
/// Recibe el motivo de baja desde una sesión iniciada. ToString oculta el texto para evitar que se registre en logs.
/// </summary>
public sealed record RequestAccountDeletionHttpRequest(string? Reason)
{
    public override string ToString() => nameof(RequestAccountDeletionHttpRequest);
}
