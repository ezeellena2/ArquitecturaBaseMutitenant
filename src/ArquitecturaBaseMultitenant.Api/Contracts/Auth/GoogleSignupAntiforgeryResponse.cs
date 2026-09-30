namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Token de formulario de un solo origen para iniciar Registro con Google.</summary>
public sealed record GoogleSignupAntiforgeryResponse(string RequestToken)
{
    public override string ToString() => nameof(GoogleSignupAntiforgeryResponse);
}
