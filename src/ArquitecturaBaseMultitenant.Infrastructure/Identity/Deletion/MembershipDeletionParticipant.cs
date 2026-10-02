using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

/// <summary>Retira una membresía de la organización durante la baja de una cuenta y deja su auditoría en la transacción ya abierta. Exige que el worker haya entrado primero al tenant correcto.</summary>
internal sealed class MembershipDeletionParticipant(ApplicationDbContext database, ITenantContext tenantContext,
    ILoginMethodReader loginMethods, IInvitationRepository invitations, IRoleAssignmentRepository assignments,
    ITenantAdminLock tenantAdmins, ILastTenantAdminGuard tenantAdminGuard, IAuditLog audit) : AccountDeletionParticipant
{
    public override async Task<IReadOnlyList<ArquitecturaBaseMultitenant.Domain.Results.Error>> CheckAsync(
        AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not null) return [];
        var result = await tenantAdminGuard.CheckCanLeaveAsync(context.UserId, ct);
        return result.IsFailure ? [result.Error] : [];
    }

    public override async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not { } tenantId) return;
        database.RequireTransaction();
        if (tenantContext.RequiredTenantId != tenantId) throw new InvalidOperationException("The deletion step needs its tenant scope.");
        if (context.TenantKind == TenantKind.Business)
        {
            var destinations = (await loginMethods.ListByUserIdAsync(context.UserId, ct))
                .Select(VerifiedEmail)
                .OfType<Email>()
                .DistinctBy(email => email.Value)
                .OrderBy(email => email.Value, StringComparer.Ordinal)
                .ToArray();
            foreach (var destination in destinations) await invitations.LockDestinationAsync(destination, ct);
            foreach (var invitation in await invitations.ListPendingByDestinationsAsync(destinations, ct))
                if (invitation.Revoke(context.NowUtc).IsFailure)
                    throw new InvalidOperationException("A checked pending invitation could not be revoked during account deletion.");
        }
        var member = await database.Members.SingleOrDefaultAsync(row => row.UserId == context.UserId, ct);
        if (member is null || member.Status == MemberStatus.Removed) return;
        if (context.TenantKind == TenantKind.Business)
        {
            await tenantAdmins.LockAssignmentsAsync(tenantId, ct);
            foreach (var assignment in await assignments.ListForMemberAsync(member.Id, ct))
                assignments.Remove(assignment);
        }
        if (member.Remove(MemberRemovalReason.AccountDeleted).IsFailure)
            throw new InvalidOperationException("The account membership could not be removed.");
        audit.Record(AuditAction.Custom, nameof(Member), member.Id,
            new Dictionary<string, object?> { ["reason"] = nameof(MemberRemovalReason.AccountDeleted), ["userId"] = context.UserId });
    }

    private static Email? VerifiedEmail(LoginMethodRow method)
    {
        if (method.VerifiedAtUtc is null) return null;
        return method.Type switch
        {
            LoginMethodType.Email => Email.Create(method.Value).Value,
            LoginMethodType.Google => method.ContactEmail,
            _ => null,
        };
    }
}
