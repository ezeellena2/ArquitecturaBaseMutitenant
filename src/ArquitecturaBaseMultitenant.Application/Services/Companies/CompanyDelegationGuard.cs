using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>
/// Nadie da lo que no tiene, en una empresa (ADR 0061 §3): lo usa <c>CompanyMembershipService</c> al sumar a alguien,
/// cambiarle los roles y cambiarle el estado, adentro de la transacción y antes de que escriba el writer. Toma primero el
/// mismo lock de la empresa que el writer (un advisory lock de la transacción, que se puede volver a pedir), relee lo
/// que tiene quien pide (<see cref="DelegationPermissionsResolver.ReadHeldAsync"/>) y las asignaciones de la persona, y
/// decide con <see cref="CompanyDelegationPolicy"/>. Una persona, un rol o una empresa que no está no se decide acá:
/// lo responde el writer. Tampoco quien no está en la empresa: sin su membresía no se miran sus roles, así la empresa
/// responde 404 por cualquiera de afuera, tenga los permisos que tenga.
/// </summary>
internal sealed class CompanyDelegationGuard(
    ICurrentUser currentUser,
    DelegationPermissionsResolver held,
    ILastCompanyAdminGuard companyLock,
    ICompanyMembershipRepository memberships,
    IMemberRepository members,
    IRoleAssignmentRepository assignments,
    IRoleReader roles)
{
    /// <summary>
    /// Sumar a alguien a la empresa con <paramref name="roleIds"/>: cada rol permite solo lo que quien pide tiene ahí; si
    /// no, <see cref="CompanyMembershipErrors.RolesNotHeld"/>.
    /// </summary>
    internal async Task<Result> CheckAddAsync(Guid companyId, IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        await companyLock.LockAsync(companyId, cancellationToken);
        return await CheckRolesAsync(companyId, [.. roleIds.Distinct()], cancellationToken);
    }

    /// <summary>
    /// Dejar a la persona con <paramref name="roleIds"/> en la empresa: quien pide tiene todos sus permisos ahí
    /// (<see cref="CompanyMembershipErrors.HasMorePermissions"/>), y cada rol que entra o sale permite solo lo que tiene
    /// ahí (<see cref="CompanyMembershipErrors.RolesNotHeld"/>).
    /// </summary>
    internal async Task<Result> CheckRolesChangeAsync(Guid companyId, Guid memberId, IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        await companyLock.LockAsync(companyId, cancellationToken);
        if (!await IsInCompanyAsync(companyId, memberId, cancellationToken)) return Result.Success();
        var current = await assignments.ListForMemberAsync(memberId, cancellationToken);
        var actedOn = await CheckActOnAsync(companyId, memberId, current, cancellationToken);
        if (actedOn.IsFailure) return actedOn;
        var before = current.Where(assignment => assignment.CompanyId == companyId)
            .Select(assignment => assignment.RoleId).ToHashSet();
        var after = roleIds.ToHashSet();
        return await CheckRolesAsync(companyId,
            [.. after.Except(before), .. before.Except(after)], cancellationToken);
    }

    /// <summary>
    /// Desactivar o reactivar a la persona en la empresa: quien pide tiene todos sus permisos ahí; si no,
    /// <see cref="CompanyMembershipErrors.HasMorePermissions"/>.
    /// </summary>
    internal async Task<Result> CheckActOnAsync(Guid companyId, Guid memberId, CancellationToken cancellationToken)
    {
        await companyLock.LockAsync(companyId, cancellationToken);
        if (!await IsInCompanyAsync(companyId, memberId, cancellationToken)) return Result.Success();
        return await CheckActOnAsync(companyId, memberId,
            await assignments.ListForMemberAsync(memberId, cancellationToken), cancellationToken);
    }

    // Quien no tiene membresía en la empresa es ajeno a ella: lo responde el writer (404), sin mirar sus roles.
    private async Task<bool> IsInCompanyAsync(Guid companyId, Guid memberId, CancellationToken cancellationToken) =>
        await memberships.FindAsync(companyId, memberId, cancellationToken) is not null;

    private async Task<Result> CheckActOnAsync(Guid companyId, Guid memberId,
        IReadOnlyList<Domain.Authorization.RoleAssignment> current, CancellationToken cancellationToken)
    {
        if (current.Count == 0) return Result.Success();
        var member = await members.GetByIdAsync(memberId, cancellationToken);
        if (member is null) return Result.Success();
        var self = member.UserId is { } userId && userId == currentUser.UserId;
        if (self) return Result.Success();
        return CompanyDelegationPolicy.CanActOn(self, await held.ReadHeldAsync(cancellationToken), companyId,
                await held.ReadAssignedAsync(current, cancellationToken))
            ? Result.Success()
            : CompanyMembershipErrors.HasMorePermissions;
    }

    private async Task<Result> CheckRolesAsync(Guid companyId, IReadOnlyCollection<Guid> changedRoleIds,
        CancellationToken cancellationToken)
    {
        if (changedRoleIds.Count == 0) return Result.Success();
        var permissions = await roles.ListPermissionsByRoleAsync(changedRoleIds, cancellationToken);
        return CompanyDelegationPolicy.CanChangeRoles(await held.ReadHeldAsync(cancellationToken), companyId,
                changedRoleIds.Select(roleId => (IEnumerable<string>)(permissions.GetValueOrDefault(roleId) ?? [])))
            ? Result.Success()
            : CompanyMembershipErrors.RolesNotHeld;
    }
}
