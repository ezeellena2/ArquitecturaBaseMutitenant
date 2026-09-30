using ArquitecturaBaseMultitenant.Api.Json;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

public sealed record CancelAccountDeletionHttpRequest([property: RawText] string? CancelTicket)
{
    public override string ToString() => nameof(CancelAccountDeletionHttpRequest);
}

public sealed record CancelAccountDeletionHttpResponse(string ReturnUrl);
