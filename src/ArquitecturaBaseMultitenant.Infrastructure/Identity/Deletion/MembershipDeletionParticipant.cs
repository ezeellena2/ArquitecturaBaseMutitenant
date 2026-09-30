using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Legal;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity.Deletion;

/// <summary>Retira una membresía de la organización durante la baja de una cuenta y deja su auditoría en la transacción ya abierta. Exige que el worker haya entrado primero al tenant correcto.</summary>
internal sealed class MembershipDeletionParticipant(ApplicationDbContext database, ITenantContext tenantContext,
    IAuditLog audit) : AccountDeletionParticipant
{
    public override async Task ExecuteAsync(AccountDeletionContext context, CancellationToken ct)
    {
        if (context.TenantId is not { } tenantId) return;
        database.RequireTransaction();
        if (tenantContext.RequiredTenantId != tenantId) throw new InvalidOperationException("The deletion step needs its tenant scope.");
        var member = await database.Members.SingleOrDefaultAsync(row => row.UserId == context.UserId, ct);
        if (member is null || member.Status == MemberStatus.Removed) return;
        if (member.Remove(MemberRemovalReason.AccountDeleted).IsFailure)
            throw new InvalidOperationException("The account membership could not be removed.");
        audit.Record(AuditAction.Custom, nameof(Member), member.Id,
            new Dictionary<string, object?> { ["reason"] = nameof(MemberRemovalReason.AccountDeleted), ["userId"] = context.UserId });
    }
}
