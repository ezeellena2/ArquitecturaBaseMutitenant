namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>Preferencias de la cuenta autenticada, nunca de un id recibido del cliente.</summary>
public sealed record UpdateMeHttpRequest(string? DisplayName, string? Culture, string? TimeZoneId)
{
    public override string ToString() => nameof(UpdateMeHttpRequest);
}
