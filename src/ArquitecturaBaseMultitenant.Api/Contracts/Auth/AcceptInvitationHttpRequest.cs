namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Acepta desde el cuerpo el token opaco y la aceptación legal requerida para una identidad nueva.</summary>
public sealed record AcceptInvitationHttpRequest(string? Token, bool AcceptedTerms)
{
    public override string ToString() => nameof(AcceptInvitationHttpRequest);
}
