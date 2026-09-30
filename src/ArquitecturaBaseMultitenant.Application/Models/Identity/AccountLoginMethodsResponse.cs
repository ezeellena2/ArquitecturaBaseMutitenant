using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record AccountLoginMethodsResponse(IReadOnlyList<AccountLoginMethodResponse> Methods,
    bool CanLinkGoogle, bool NeedsPersonalLoginMethod, int AccountDeletionGraceDays)
{
    public override string ToString() => nameof(AccountLoginMethodsResponse);
}

public sealed record AccountLoginMethodResponse(Guid Id, LoginMethodType Type, string? Value,
    bool IsPrimary, bool IsVerified, Guid? ManagedByTenantId, string? ManagedByOrganizationName,
    bool CanRemove, bool CanMakePrimary, string? BackupDestination)
{
    public override string ToString() => nameof(AccountLoginMethodResponse);
}
