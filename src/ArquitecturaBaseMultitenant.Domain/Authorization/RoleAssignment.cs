using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Authorization;

/// <summary>Asigna un rol de la organización a una membresía, opcionalmente dentro de una empresa.</summary>
public sealed class RoleAssignment : Entity, ITenantOwned, IAuditable
{
    private RoleAssignment()
    {
    }

    private RoleAssignment(Guid memberId, Guid roleId, Guid? companyId)
    {
        MemberId = memberId;
        RoleId = roleId;
        CompanyId = companyId;
    }

    public Guid MemberId { get; private set; }

    public Guid RoleId { get; private set; }

    public Guid? CompanyId { get; private set; }

    public Guid TenantId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTime? ModifiedAtUtc { get; private set; }

    public Guid? ModifiedBy { get; private set; }

    public static Result<RoleAssignment> Assign(Guid memberId, Role role, Guid? companyId)
    {
        ArgumentNullException.ThrowIfNull(role);
        if (memberId == Guid.Empty) throw new ArgumentException("The member id cannot be empty.", nameof(memberId));
        if (companyId == Guid.Empty) throw new ArgumentException("The company id cannot be empty.", nameof(companyId));

        if (role.Scope == RoleScope.Organization)
            return companyId is null ? new RoleAssignment(memberId, role.Id, null) : RoleAssignmentErrors.CompanyNotAllowed;

        if (companyId is null) return RoleAssignmentErrors.CompanyRequired;
        if (role.Scope == RoleScope.SpecificCompany && role.CompanyId != companyId)
            return RoleAssignmentErrors.CompanyMismatch;

        return new RoleAssignment(memberId, role.Id, companyId);
    }
}
