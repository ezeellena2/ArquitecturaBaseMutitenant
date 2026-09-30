namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Devuelve el ticket temporal para completar una acción sensible sin mostrarlo en diagnósticos del objeto.</summary>
public sealed record ReauthResponse(string ReauthTicket)
{
    public override string ToString() => nameof(ReauthResponse);
}
