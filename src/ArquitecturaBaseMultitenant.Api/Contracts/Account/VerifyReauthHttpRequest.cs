using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

public sealed record VerifyReauthHttpRequest(ReauthAction? Action, Guid? TargetMethodId, Guid? SourceMethodId,
    [property: RawText] string? Code)
{
    public override string ToString() => nameof(VerifyReauthHttpRequest);
}
