using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record VerifyReauthRequest(ReauthAction Action, Guid? TargetMethodId, Guid SourceMethodId, string? Code)
{
    public override string ToString() => nameof(VerifyReauthRequest);
}
