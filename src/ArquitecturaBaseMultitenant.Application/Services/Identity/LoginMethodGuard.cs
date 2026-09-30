using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

internal sealed class LoginMethodGuard(ILoginMethodRepository methods, IUserRepository users)
{
    public async Task<Result<UserAccountRow>> LockAccountAsync(Guid userId, CancellationToken ct)
    {
        await methods.LockUserAsync(userId, ct);
        var account = await users.GetByIdAsync(userId, ct);
        if (account is null) return UserErrors.NotFound;
        if (account.Status is UserStatus.Suspended or UserStatus.Deleted) return AccountErrors.Suspended;
        return account.Status == UserStatus.PendingDeletion ? AccountErrors.PendingDeletion : account;
    }
}
