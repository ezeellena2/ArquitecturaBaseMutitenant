using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

internal sealed class InvitationAcceptor(IInvitationRepository invitations, IMemberRepository members,
    ITenantReader tenants, InvitationAcceptanceGuard guard, InvitationAccountRegistrar registrar,
    IInvitationTokenProtector tokens, TimeProvider timeProvider)
{
    internal async Task<Result<InvitationAcceptanceOutcome>> AcceptAsync(InvitationAcceptanceContext context, CancellationToken ct)
    {
        await guard.LockAsync(context, ct);
        var invitation = await invitations.GetByIdAsync(context.Proof.InvitationId, ct);
        if (invitation is null || invitation.Destination != context.Destination) return InvitationErrors.Invalid;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var valid = invitation.CheckToken(context.TokenHash, nowUtc);
        if (valid.IsFailure) return valid.Error;
        var tenant = await tenants.FindByIdAsync(context.Proof.TenantId, ct);
        if (tenant is null || tenant.Kind != TenantKind.Business) return InvitationErrors.Invalid;
        if (tenant.Status != TenantStatus.Active)
            return tenant.Status == TenantStatus.Suspended ? TenantErrors.Suspended : InvitationErrors.Invalid;
        var member = await members.GetByIdAsync(invitation.MemberId, ct);
        if (member is null || member.Status != MemberStatus.Invited) return InvitationErrors.Invalid;
        var owner = await guard.CheckOwnerAsync(context, ct);
        if (owner.IsFailure) return owner.Error;
        if (member.UserId is { } bound && bound != owner.Value) return InvitationErrors.WrongAccount;
        if (owner.Value is { } existing)
        {
            var other = await members.GetByUserIdAsync(existing, ct);
            if (other is not null && other.Id != member.Id) return InvitationErrors.Invalid;
        }
        var created = owner.Value is null;
        var userId = owner.Value ?? await registrar.RegisterAsync(invitation.Destination, nowUtc, ct);
        if (!created) await registrar.EnsureEmailAsync(userId, invitation.Destination, nowUtc, ct);
        if (member.UserId is null && member.AssignUser(userId).IsFailure)
            throw new InvalidOperationException("A checked invitation member could not be bound.");
        if (member.Activate(nowUtc).IsFailure || invitation.Accept(userId, context.TokenHash, nowUtc,
                created ? tokens.Hash(context.Nonce) : null).IsFailure)
            throw new InvalidOperationException("A checked invitation could not be accepted.");
        return new InvitationAcceptanceOutcome(userId, context.Proof.TenantId, created, invitation.ExpiresAtUtc);
    }
}
