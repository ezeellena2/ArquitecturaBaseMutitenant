using ArquitecturaBaseMultitenant.Api.Json;
namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

public sealed record PreviewLoginLinkHttpRequest([property: RawText] string? Token)
{
    public override string ToString() => nameof(PreviewLoginLinkHttpRequest);
}
