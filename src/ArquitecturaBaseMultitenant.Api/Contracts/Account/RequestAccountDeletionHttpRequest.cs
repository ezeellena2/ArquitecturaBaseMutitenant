using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

public sealed record RequestAccountDeletionHttpRequest(string? Reason, [property: RawText] string? ReauthTicket)
{
    public override string ToString() => nameof(RequestAccountDeletionHttpRequest);
}
