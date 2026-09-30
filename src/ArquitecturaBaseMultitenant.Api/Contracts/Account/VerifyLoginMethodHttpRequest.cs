using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

public sealed record VerifyLoginMethodHttpRequest([property: RawText] string? Code)
{
    public override string ToString() => nameof(VerifyLoginMethodHttpRequest);
}
