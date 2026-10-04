using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;

/// <summary>Escrituras de autorización global dentro del límite transaccional del caso de uso.</summary>
internal sealed class PlatformPermissionAssignmentRepository(ApplicationDbContext context)
    : IPlatformPermissionAssignmentRepository
{
    public Task<PlatformPermissionAssignment?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return context.PlatformPermissionAssignments.SingleOrDefaultAsync(
            assignment => assignment.UserId == userId, cancellationToken);
    }

    public void Add(PlatformPermissionAssignment assignment)
    {
        context.RequireTransaction();
        context.PlatformPermissionAssignments.Add(assignment);
    }

    public void Remove(PlatformPermissionAssignment assignment)
    {
        context.RequireTransaction();
        context.PlatformPermissionAssignments.Remove(assignment);
    }

    public Task LockOperatorsAsync(CancellationToken cancellationToken) =>
        context.AcquireAdvisoryLocksAsync([AdvisoryLockKeys.PlatformOperators], cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListActiveManagerIdsAsync(CancellationToken cancellationToken)
    {
        context.RequireTransaction();
        return await (from assignment in context.PlatformPermissionAssignments.AsNoTracking()
                      join user in context.Users.AsNoTracking() on assignment.UserId equals user.Id
                      // ADR 0042: una invitación sin aceptar todavía no administra operadores.
                      where user.Status == UserStatus.Active && user.IsPlatformOperator
                          && user.OperatorInvitationExpiresAtUtc == null
                          && assignment.Permissions.Contains(PlatformPermissions.Operators.Manage)
                      select assignment.UserId).ToArrayAsync(cancellationToken);
    }
}
