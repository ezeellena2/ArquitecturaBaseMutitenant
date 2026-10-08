using ArquitecturaBaseMultitenant.Application.Configuration.Invitations;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>Emite dentro de la transacción del llamador y encola el correo en su outbox; no tiene ruta propia.</summary>
internal sealed class InvitationIssuer(IInvitationRepository invitations, IMemberRepository members,
    ITenantContext tenantContext, IInvitationTokenProtector tokens, InvitationIssuingGuard guard,
    InvitationDeliveryIssuer delivery, IOptions<InvitationOptions> options, TimeProvider timeProvider)
{
    public async Task<Result<Guid>> IssueAsync(IssueInvitationRequest request, CancellationToken cancellationToken)
    {
        var issued = await IssueWithMemberAsync(request, cancellationToken);
        return issued.IsFailure ? issued.Error : issued.Value.InvitationId;
    }

    public async Task<Result<IssuedInvitation>> IssueWithMemberAsync(IssueInvitationRequest request,
        CancellationToken cancellationToken, TenantSettings? preparedSettings = null, InvitationGrantRow? grant = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Destination);
        if (request.InviterUserId == Guid.Empty) throw new ArgumentException("An inviter is required.", nameof(request));
        if (request.MemberId == Guid.Empty) throw new ArgumentException("The member id cannot be empty.", nameof(request));
        var tenantId = tenantContext.RequiredTenantId;
        if (!delivery.IsAvailable(request.Channel)) return InvitationErrors.ChannelUnavailable;
        var checkedRequest = await guard.CheckAsync(tenantId, request, cancellationToken);
        if (checkedRequest.IsFailure) return checkedRequest.Error;
        var pendingInvitation = await invitations.GetPendingByDestinationAsync(request.Destination, cancellationToken);
        if (pendingInvitation is not null && pendingInvitation.Id != request.ReplacingInvitationId)
            return InvitationErrors.AlreadyPending;
        var (organization, userId) = checkedRequest.Value;
        var destination = await delivery.ResolveAsync(request.Channel, request.Destination, userId, cancellationToken);
        if (destination.IsFailure) return destination.Error;
        if (userId is { } knownUserId) await members.LockUserAsync(knownUserId, cancellationToken);
        var member = request.MemberId is { } requestedMemberId
            ? await members.GetByIdAsync(requestedMemberId, cancellationToken)
            : userId is { } recipientId
                ? await members.GetByUserIdAsync(recipientId, cancellationToken) : null;
        if (request.MemberId is not null && member is null) return MemberErrors.NotFound;
        if (member?.UserId is { } boundUserId && userId != boundUserId) return InvitationErrors.WrongAccount;
        if (member is null)
        {
            member = userId is { } existingId ? Member.Invite(existingId) : Member.Invite();
            members.Add(member);
        }
        else if (member.PrepareForInvitation().IsFailure)
        {
            return MemberErrors.InvalidTransition;
        }
        var secret = tokens.GenerateSecret();
        var invitation = Invitation.Issue(member.Id, request.InviterUserId, request.Destination, request.Channel,
            tokens.Hash(secret), timeProvider.GetUtcNow().UtcDateTime, TimeSpan.FromDays(options.Value.LifetimeDays));
        invitations.Add(invitation);
        var token = tokens.Protect(new InvitationTokenData(tenantId, invitation.Id, secret));
        await delivery.EnqueueAsync(invitation, organization, token, userId, destination.Value, cancellationToken, preparedSettings, grant);
        return new IssuedInvitation(invitation.Id, member, invitation.ExpiresAtUtc);
    }
}
