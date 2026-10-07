namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Identifica el método propio que se cambiará o quitará.</summary>
public sealed record ChangeLoginMethodRequest(Guid MethodId)
{
    public override string ToString() => nameof(ChangeLoginMethodRequest);
}
