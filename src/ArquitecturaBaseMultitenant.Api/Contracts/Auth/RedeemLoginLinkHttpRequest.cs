using ArquitecturaBaseMultitenant.Api.Json;
namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

public sealed record RedeemLoginLinkHttpRequest([property: RawText] string? Token, string? Access)
{
    public override string ToString() => nameof(RedeemLoginLinkHttpRequest);
}
