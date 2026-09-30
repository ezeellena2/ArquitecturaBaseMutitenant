namespace ArquitecturaBaseMultitenant.Application.Models.Profile;

/// <summary>El usuario sale de la sesión, nunca del cuerpo del pedido.</summary>
public sealed record UpdateMeRequest(string? DisplayName, string? Culture, string? TimeZoneId, uint? Version)
{
    public override string ToString() => nameof(UpdateMeRequest);
}
