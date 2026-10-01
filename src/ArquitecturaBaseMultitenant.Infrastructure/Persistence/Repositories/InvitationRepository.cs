using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Prepara invitaciones en la transacción vigente; los locks serializan aceptación y destinos compartidos.</summary>
internal sealed class InvitationRepository(ApplicationDbContext context, ITenantContext tenantContext) : IInvitationRepository
{
    public Task LockAsync(Guid invitationId, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync(
            [AdvisoryLockKeys.For(tenantContext.RequiredTenantId, "invitation", invitationId)], cancellationToken);

    public Task LockDestinationAsync(Email destination, CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.LoginCode(LoginCodeDestination.ForEmail(destination))], cancellationToken);

    public Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        return context.Invitations.SingleOrDefaultAsync(invitation => invitation.Id == id, cancellationToken);
    }

    public Task<Invitation?> GetPendingByDestinationAsync(Email destination, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        return context.Invitations.SingleOrDefaultAsync(invitation => invitation.Destination == destination
            && invitation.Status == InvitationStatus.Pending, cancellationToken);
    }

    public void Add(Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        context.RequireTransaction();
        _ = tenantContext.RequiredTenantId;
        context.Invitations.Add(invitation);
    }
}
