using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Única omisión nominal del filtro Tenant para reservas demostradas por OTP; RLS sigue forzado.</summary>
internal sealed class LoginInvitationRepository(ApplicationDbContext db, LoginInvitationScope scope,
    ILoginInvitationReader reader, TimeProvider clock)
    : ILoginInvitationRepository
{
    public async Task<IReadOnlyList<(Invitation Invitation, Member Member)>> LockAndReadAsync(
        LoginInvitationsContext context, CancellationToken ct)
    {
        db.RequireTransaction();
        // Relee el índice bajo el lock del destino: incluye altas confirmadas mientras se preparaba el ingreso.
        var pending = await reader.ListPendingAsync(context.Destination, clock.GetUtcNow().UtcDateTime, ct);
        var locks = pending.Select(row => AdvisoryLockKeys.For(row.TenantId, "invitation", row.InvitationId)).ToList();
        if (context.ExpectedOwnerId is { } userId)
            locks.AddRange(pending.Select(row => AdvisoryLockKeys.For(row.TenantId, "member-user", userId)));
        await db.AcquireAdvisoryLocksAsync(locks, ct);
        var keys = string.Join(',', pending.Select(row => $"{row.TenantId:D}:{row.InvitationId:D}"));
        var memberKeys = string.Join(',', pending.Select(row => $"{row.TenantId:D}:{row.MemberId:D}"));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.login_invitation_destination', {context.Destination.Value}, true), set_config('app.login_invitation_keys', {keys}, true), set_config('app.login_invitation_member_keys', {memberKeys}, true)", ct);
        scope.Authorize(pending);
        var invitations = await db.Invitations.IgnoreQueryFilters(["Tenant"])
            .Where(row => row.Destination == context.Destination && pending.Select(candidate => candidate.InvitationId).Contains(row.Id))
            .OrderBy(row => row.TenantId).ThenBy(row => row.Id).ToArrayAsync(ct);
        var members = await db.Members.IgnoreQueryFilters(["Tenant"])
            .Where(row => pending.Select(candidate => candidate.MemberId).Contains(row.Id)).ToArrayAsync(ct);
        return invitations.Select(invitation => (invitation,
            members.Single(member => member.TenantId == invitation.TenantId && member.Id == invitation.MemberId))).ToArray();
    }

    public Task BindVerifiedUserAsync(Guid userId, CancellationToken ct)
    {
        db.RequireTransaction();
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('app.login_invitation_user_id', {userId.ToString("D")}, true)", ct);
    }
}
