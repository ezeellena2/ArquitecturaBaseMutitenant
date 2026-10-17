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
        => await LockAsync(new LoginInvitationContext(context.Proof.InvitationId, context.Proof.TenantId,
            context.Destination, context.ExpectedOwnerId), ct);

    internal async Task LockAsync(LoginInvitationContext context, CancellationToken ct)
    {
        await LockDestinationAsync(context.Destination, context.ExpectedOwnerId, ct);
        await invitations.LockAsync(context.InvitationId, ct);
        if (context.ExpectedOwnerId is { } existing) await members.LockUserAsync(existing, ct);
    }

    internal async Task LockDestinationAsync(InvitationIdentity destination, Guid? expectedOwnerId, CancellationToken ct)
    {
        await externalLock.LockIdentityAsync(destination, ct);
        if (expectedOwnerId is { } owner) await methods.LockUserAsync(owner, ct);
        await invitations.LockDestinationAsync(destination, ct);
    }

    internal async Task<Result<Guid?>> CheckOwnerAsync(InvitationAcceptanceContext context, CancellationToken ct)
        => await CheckOwnerAsync(context.Destination, context.ExpectedOwnerId, context.CurrentUserId, ct);

    internal Task<Result<Guid?>> CheckVerifiedOwnerAsync(LoginInvitationContext context, CancellationToken ct) =>
        CheckOwnerAsync(context.Destination, context.ExpectedOwnerId, context.ExpectedOwnerId, ct);

    private async Task<Result<Guid?>> CheckOwnerAsync(InvitationIdentity destination, Guid? expectedOwnerId, Guid? currentUserId,
        CancellationToken ct)
    {
        var owners = await lookup.FindOwnersAsync(destination, ct);
        if (owners.Count > 1) return InvitationErrors.WrongAccount;
        Guid? owner = owners.Count == 0 ? null : owners[0];
        if (owner != expectedOwnerId)
            return currentUserId is null ? InvitationErrors.SignInRequired : InvitationErrors.WrongAccount;
        if (currentUserId is not null && currentUserId != owner) return InvitationErrors.WrongAccount;
        if (owner is { } existing)
        {
            var user = await users.GetByIdAsync(existing, ct);
            if (user is null || user.Status is UserStatus.Suspended or UserStatus.Deleted) return AccountErrors.Suspended;
            if (user.IsPlatformOperator) return InvitationErrors.PlatformOperator;
            if (user.Status == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;
            if (currentUserId is null) return InvitationErrors.SignInRequired;
            if (await signIn.IsLockedOutAsync(existing, ct)) return AccountErrors.LockedOut;
        }
        return Result.Success(owner);
    }
}
