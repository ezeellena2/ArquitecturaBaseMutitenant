using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Legal;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Legal;

/// <summary>Retira una membresía de la organización durante la baja de una cuenta, en la transacción ya abierta. Su auditoría es la de cualquier salida, «Quitó a …» con el motivo, y la deja el interceptor. Exige que el worker haya entrado primero al tenant correcto.</summary>
internal sealed class MembershipDeletionParticipant(IAccountDeletionCleanupRepository cleanup, ITenantContext tenantContext,
    ILoginMethodReader loginMethods, IInvitationRepository invitations, IRoleAssignmentRepository assignments,
    ITenantAdminLock tenantAdmins, ILastTenantAdminGuard tenantAdminGuard) : IAccountDeletionParticipant
{
    public async Task<IReadOnlyList<Error>> CheckAsync(
        AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not null) return [];
        var result = await tenantAdminGuard.CheckCanLeaveAsync(context.UserId, ct);
        return result.IsFailure ? [result.Error] : [];
    }

    public Task OnRequestedAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;
    public Task OnCancelledAsync(AccountDeletionContext context, CancellationToken ct) => Task.CompletedTask;

    public async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not { } tenantId) return;
        cleanup.RequireTransaction();
        if (tenantContext.RequiredTenantId != tenantId) throw new InvalidOperationException("The deletion step needs its tenant scope.");
        if (context.TenantKind == TenantKind.Business)
        {
            var own = await loginMethods.ListByUserIdAsync(context.UserId, ct);
            var destinations = own.Select(VerifiedEmail).OfType<Email>().Select(InvitationIdentity.ForEmail)
                .Concat(own.Where(method => method.Type == LoginMethodType.Phone && method.VerifiedAtUtc is not null)
                    .Select(method => PhoneNumber.Create(method.Value)).Where(phone => phone.IsSuccess)
                    .Select(phone => InvitationIdentity.ForPhone(phone.Value)))
                .DistinctBy(destination => destination.Value)
                .OrderBy(destination => destination.Value, StringComparer.Ordinal)
                .ToArray();
            foreach (var destination in destinations) await invitations.LockDestinationAsync(destination, ct);
            foreach (var invitation in await invitations.ListPendingByDestinationsAsync(destinations, ct))
                if (invitation.Revoke(context.NowUtc).IsFailure)
                    throw new InvalidOperationException("A checked pending invitation could not be revoked during account deletion.");
        }
        var member = await cleanup.GetMemberAsync(context.UserId, ct);
        if (member is null || member.Status == MemberStatus.Removed) return;
        if (context.TenantKind == TenantKind.Business)
        {
            await tenantAdmins.LockAssignmentsAsync(tenantId, ct);
            foreach (var assignment in await assignments.ListForMemberAsync(member.Id, ct))
                assignments.Remove(assignment);
        }
        if (member.Remove(MemberRemovalReason.AccountDeleted).IsFailure)
            throw new InvalidOperationException("The account membership could not be removed.");
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
