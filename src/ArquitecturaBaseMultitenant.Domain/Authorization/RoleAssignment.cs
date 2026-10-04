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

    /// <summary>
    /// Dónde vale un rol: el de la organización, sin empresa; el de cada empresa, en cualquiera; el de una empresa,
    /// solo en la suya. Devuelve el error, o null si el rol vale en <paramref name="companyId"/> (null: la
    /// organización). Es la única pieza de la regla: la usan Usuarios, Empresas y el alta de cada organización.
    /// </summary>
    public static Error? Check(RoleScope scope, Guid? roleCompanyId, Guid? companyId) => scope switch
    {
        RoleScope.Organization => companyId is null ? null : RoleAssignmentErrors.CompanyNotAllowed,
        _ when companyId is null => RoleAssignmentErrors.CompanyRequired,
        RoleScope.SpecificCompany when roleCompanyId != companyId => RoleAssignmentErrors.CompanyMismatch,
        _ => null,
    };

    /// <summary>Asigna el rol con los datos de su alcance, sin cargar la entidad: la regla es <see cref="Check"/>.</summary>
    public static Result<RoleAssignment> Assign(Guid memberId, Guid roleId, RoleScope scope, Guid? roleCompanyId,
        Guid? companyId)
    {
        if (memberId == Guid.Empty) throw new ArgumentException("The member id cannot be empty.", nameof(memberId));
        if (roleId == Guid.Empty) throw new ArgumentException("The role id cannot be empty.", nameof(roleId));
        if (companyId == Guid.Empty) throw new ArgumentException("The company id cannot be empty.", nameof(companyId));

        return Check(scope, roleCompanyId, companyId) is { } error
            ? error
            : new RoleAssignment(memberId, roleId, companyId);
    }
}
