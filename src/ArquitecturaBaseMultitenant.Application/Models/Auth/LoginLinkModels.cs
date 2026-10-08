namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

public sealed record PreviewLoginLinkRequest(string? Token)
{
    public override string ToString() => nameof(PreviewLoginLinkRequest);
}

public sealed record RedeemLoginLinkRequest(string? Token, string? Access)
{
    public override string ToString() => nameof(RedeemLoginLinkRequest);
}

public sealed record LoginLinkPreviewResponse(string? DisplayName, string? MaskedPhone);
public sealed record LoginLinkRedeemResponse(string Access);

/// <summary>El secreto se entrega solo al adaptador que encola el mensaje cifrado; nunca a logs.</summary>
public sealed record IssuedLoginLink(string Token, DateTime ExpiresAtUtc)
{
    public override string ToString() => nameof(IssuedLoginLink);
}
