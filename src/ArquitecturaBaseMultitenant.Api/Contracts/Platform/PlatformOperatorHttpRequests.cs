using ArquitecturaBaseMultitenant.Application.Models.Platform;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed record InvitePlatformOperatorHttpRequest(string? Email, IReadOnlyList<string>? Permissions)
{
    public override string ToString() => nameof(InvitePlatformOperatorHttpRequest);
}

public sealed record ChangePlatformOperatorHttpRequest(uint? Version, string? Reason, IReadOnlyList<string>? Permissions)
{
    public ChangePlatformOperatorRequest ToModel() => new(Version, Reason, Permissions);
    public override string ToString() => nameof(ChangePlatformOperatorHttpRequest);
}
