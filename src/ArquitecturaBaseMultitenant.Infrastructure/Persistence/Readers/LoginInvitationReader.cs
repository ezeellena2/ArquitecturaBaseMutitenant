using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;

/// <summary>Consulta sólo la proyección del destino solicitado; no revela miembros, grants ni tokens.</summary>
internal sealed class LoginInvitationReader(ApplicationDbContext context) : ILoginInvitationReader
{
    public async Task<IReadOnlyList<LoginInvitationRow>> ListPendingAsync(Email destination, DateTime nowUtc, CancellationToken ct) =>
        await (from invitation in context.LoginInvitations.AsNoTracking()
               join tenant in context.Tenants.AsNoTracking() on invitation.TenantId equals tenant.Id
               where invitation.Destination == destination && invitation.ExpiresAtUtc > nowUtc && tenant.Kind == TenantKind.Business
               orderby invitation.IssuedAtUtc, tenant.Name, tenant.Id
               select new LoginInvitationRow(invitation.InvitationId, invitation.MemberId, tenant.Id, invitation.IssuedAtUtc, tenant.Name)).ToArrayAsync(ct);
}
