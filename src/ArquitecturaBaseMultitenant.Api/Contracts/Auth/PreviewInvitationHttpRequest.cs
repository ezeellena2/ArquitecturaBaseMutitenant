namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Token opaco recibido en el cuerpo; nunca se expone en la ruta, la query ni el diagnóstico.</summary>
public sealed record PreviewInvitationHttpRequest(string? Token)
{
    public override string ToString() => nameof(PreviewInvitationHttpRequest);
}
