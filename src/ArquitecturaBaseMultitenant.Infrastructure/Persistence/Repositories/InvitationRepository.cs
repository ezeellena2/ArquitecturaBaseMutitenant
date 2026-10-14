using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara invitaciones en la transacción vigente; los locks serializan aceptación y destinos compartidos.</summary>
internal sealed class InvitationRepository(ApplicationDbContext context, ITenantContext tenantContext) : IInvitationRepository
{
    public Task LockAsync(Guid invitationId, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync(
            [AdvisoryLockKeys.For(tenantContext.RequiredTenantId, "invitation", invitationId)], cancellationToken);

    public Task LockDestinationAsync(InvitationIdentity destination, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.LoginCode(destination)], cancellationToken);

    public Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        return context.Invitations.SingleOrDefaultAsync(invitation => invitation.Id == id, cancellationToken);
    }

    public Task<Invitation?> GetPendingByDestinationAsync(InvitationIdentity destination, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        return context.Invitations.SingleOrDefaultAsync(invitation => invitation.Destination == destination
            && invitation.Status == InvitationStatus.Pending, cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> ListPendingByDestinationsAsync(
        IReadOnlyCollection<InvitationIdentity> destinations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destinations);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        if (destinations.Count == 0) return [];
        return await context.Invitations.Where(invitation => invitation.Status == InvitationStatus.Pending
            && destinations.Contains(invitation.Destination)).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> ListPendingForMemberAsync(Guid memberId,
        CancellationToken cancellationToken)
    {
        if (memberId == Guid.Empty) throw new ArgumentException("A member ID is required.", nameof(memberId));
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        return await context.Invitations.Where(invitation => invitation.MemberId == memberId
            && invitation.Status == InvitationStatus.Pending).ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Invitation>> ListUnrecordedExpiriesAsync(DateTime nowUtc, int limit,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var tenantId = tenantContext.RequiredTenantId;
        await context.AcquireAdvisoryLocksAsync(
            [AdvisoryLockKeys.For(tenantId, "invitation-expiry", tenantId)], cancellationToken);
        // La regla de Invitation.RecordExpiry: pendiente al vencer, aunque después se la haya revocado.
        return await context.Invitations.Where(invitation => invitation.ExpiryRecordedAtUtc == null
                && invitation.ExpiresAtUtc <= nowUtc
                && (invitation.Status == InvitationStatus.Pending || invitation.Status == InvitationStatus.Revoked
                    && invitation.RevokedAtUtc >= invitation.ExpiresAtUtc))
            .OrderBy(invitation => invitation.ExpiresAtUtc).ThenBy(invitation => invitation.Id)
            .Take(limit).ToArrayAsync(cancellationToken);
    }

    public void Add(Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Invitations.Add(invitation);
    }
}
