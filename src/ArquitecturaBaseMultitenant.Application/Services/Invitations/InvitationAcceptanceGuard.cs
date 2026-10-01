using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

internal sealed class InvitationAcceptanceGuard(IExternalLoginLock externalLock, ILoginMethodRepository methods,
    IInvitationRepository invitations, IMemberRepository members, IUserLookup lookup, IUserRepository users,
    ISignInService signIn)
{
    internal async Task LockAsync(InvitationAcceptanceContext context, CancellationToken ct)
    {
        await externalLock.LockEmailAsync(context.Destination, ct);
        if (context.ExpectedOwnerId is { } owner) await methods.LockUserAsync(owner, ct);
        await invitations.LockDestinationAsync(context.Destination, ct);
        await invitations.LockAsync(context.Proof.InvitationId, ct);
        if (context.ExpectedOwnerId is { } existing) await members.LockUserAsync(existing, ct);
    }

    internal async Task<Result<Guid?>> CheckOwnerAsync(InvitationAcceptanceContext context, CancellationToken ct)
    {
        var owners = await lookup.FindVerifiedUsersByEmailAsync(context.Destination, ct);
        if (owners.Count > 1) return InvitationErrors.WrongAccount;
        Guid? owner = owners.Count == 0 ? null : owners[0];
        if (owner != context.ExpectedOwnerId)
            return context.CurrentUserId is null ? InvitationErrors.SignInRequired : InvitationErrors.WrongAccount;
        if (context.CurrentUserId is not null && context.CurrentUserId != owner) return InvitationErrors.WrongAccount;
        if (owner is { } existing)
        {
            var user = await users.GetByIdAsync(existing, ct);
            if (user is null || user.Status is UserStatus.Suspended or UserStatus.Deleted) return AccountErrors.Suspended;
            if (user.Status == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
            if (context.CurrentUserId is null) return InvitationErrors.SignInRequired;
            if (await signIn.IsLockedOutAsync(existing, ct)) return AccountErrors.LockedOut;
        }
        return Result.Success(owner);
    }
}
