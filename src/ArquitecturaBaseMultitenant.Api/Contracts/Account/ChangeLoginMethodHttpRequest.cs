using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

public sealed record ChangeLoginMethodHttpRequest([property: RawText] string? ReauthTicket)
{
    public override string ToString() => nameof(ChangeLoginMethodHttpRequest);
}
