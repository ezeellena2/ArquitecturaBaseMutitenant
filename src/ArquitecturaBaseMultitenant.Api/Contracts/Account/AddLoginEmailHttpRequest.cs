namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

public sealed record AddLoginEmailHttpRequest(string? Email)
{
    public override string ToString() => nameof(AddLoginEmailHttpRequest);
}
