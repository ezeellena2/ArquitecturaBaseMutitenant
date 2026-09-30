namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Identifica el método que se cambiará o quitará y lleva la prueba de reautenticación; su representación textual oculta el ticket.</summary>
public sealed record ChangeLoginMethodRequest(Guid MethodId, string? ReauthTicket)
{
    public override string ToString() => nameof(ChangeLoginMethodRequest);
}
