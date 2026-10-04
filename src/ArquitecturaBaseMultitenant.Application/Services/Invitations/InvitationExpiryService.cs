using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>
/// «Venció la invitación a …» (tablero Auditoria-Org): cada invitación que venció pendiente deja una entrada, una sola
/// vez, con la fecha en que venció. Quien actúa es el sistema, porque lo llama un trabajo sin usuario. La entrada lleva
/// las empresas de la persona invitada, para que «Dónde» las nombre.
/// </summary>
internal sealed class InvitationExpiryService(
    IInvitationRepository invitations,
    ICompanyMembershipRepository memberships,
    IAuditLog audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<InvitationExpiryService> logger) : IInvitationExpiryService
{
    private const int BatchSize = 100;

    public Task<Result<int>> RecordExpiredAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "RecordExpiredInvitations", () =>
            unitOfWork.ExecuteInTransactionAsync<Result<int>>(async ct =>
            {
                var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                var expired = await invitations.ListUnrecordedExpiriesAsync(nowUtc, BatchSize, ct);
                var companies = new Dictionary<Guid, Guid[]>();
                foreach (var memberId in expired.Select(invitation => invitation.MemberId).Distinct())
                    companies[memberId] =
                    [
                        .. (await memberships.ListForMemberAsync(memberId, ct))
                            .Where(membership => membership.Status == CompanyMembershipStatus.Active)
                            .Select(membership => membership.CompanyId),
                    ];

                var recorded = 0;
                foreach (var invitation in expired)
                {
                    if (!invitation.RecordExpiry(nowUtc)) continue;
                    audit.Record(AuditAction.Updated, nameof(Invitation), invitation.Id,
                        Changes(companies[invitation.MemberId]), invitation.ExpiresAtUtc);
                    recorded++;
                }

                return recorded;
            }, CommitPolicy.OnSuccess, cancellationToken));

    private static Dictionary<string, object?> Changes(Guid[] companyIds)
    {
        var changes = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [nameof(Invitation.Status)] = Change(nameof(InvitationStatus.Pending), AuditChangeKeys.ExpiredInvitationStatus),
        };
        if (companyIds.Length > 0) changes[AuditChangeKeys.CompanyIds] = Change(null, companyIds);
        return changes;
    }

    private static Dictionary<string, object?> Change(object? oldValue, object? newValue) => new(StringComparer.Ordinal)
    {
        ["old"] = oldValue,
        ["new"] = newValue,
    };
}
