using ArquitecturaBaseMultitenant.Application.Configuration.Invitations;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>Emite dentro de la transacción del llamador y encola el correo en su outbox; no tiene ruta propia.</summary>
internal sealed class InvitationIssuer(IInvitationRepository invitations, IMemberRepository members,
    ITenantContext tenantContext, IInvitationTokenProtector tokens, InvitationIssuingGuard guard,
    InvitationDeliveryIssuer delivery, IOptions<InvitationOptions> options, TimeProvider timeProvider)
{
    public async Task<Result<Guid>> IssueAsync(IssueInvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Destination);
        if (request.InviterUserId == Guid.Empty) throw new ArgumentException("An inviter is required.", nameof(request));
        var tenantId = tenantContext.RequiredTenantId;
        if (!delivery.IsAvailable(request.Channel)) return InvitationErrors.ChannelUnavailable;
        var checkedRequest = await guard.CheckAsync(tenantId, request, cancellationToken);
        if (checkedRequest.IsFailure) return checkedRequest.Error;
        if (await invitations.GetPendingByDestinationAsync(request.Destination, cancellationToken) is not null)
            return InvitationErrors.AlreadyPending;
        var (organization, userId) = checkedRequest.Value;
        if (userId is { } knownUserId) await members.LockUserAsync(knownUserId, cancellationToken);
        var member = userId is { } recipientId
            ? await members.GetByUserIdAsync(recipientId, cancellationToken) : null;
        if (member is not null && member.Status != MemberStatus.Invited) return MemberErrors.InvalidTransition;
        if (member is null)
        {
            member = userId is { } existingId ? Member.Invite(existingId) : Member.Invite();
            members.Add(member);
        }
        var secret = tokens.GenerateSecret();
        var invitation = Invitation.Issue(member.Id, request.InviterUserId, request.Destination, request.Channel,
            tokens.Hash(secret), timeProvider.GetUtcNow().UtcDateTime, TimeSpan.FromDays(options.Value.LifetimeDays));
        invitations.Add(invitation);
        var token = tokens.Protect(new InvitationTokenData(tenantId, invitation.Id, secret));
        await delivery.EnqueueAsync(invitation, organization, token, userId, cancellationToken);
        return invitation.Id;
    }
}
