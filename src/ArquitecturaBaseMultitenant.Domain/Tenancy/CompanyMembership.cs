using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Tenancy;

/// <summary>Vincula un miembro de la organización con una empresa y conserva el estado de acceso a esa empresa.</summary>
public sealed class CompanyMembership : Entity, ITenantOwned, IAuditable, IVersioned
{
    private CompanyMembership()
    {
    }

    private CompanyMembership(Guid companyId, Guid memberId)
    {
        CompanyId = companyId;
        MemberId = memberId;
        Status = CompanyMembershipStatus.Active;
    }

    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid MemberId { get; private set; }
    public CompanyMembershipStatus Status { get; private set; }
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }

    public static Result<CompanyMembership> Create(Guid companyId, Guid memberId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("The company id cannot be empty.", nameof(companyId));
        if (memberId == Guid.Empty) throw new ArgumentException("The member id cannot be empty.", nameof(memberId));
        return new CompanyMembership(companyId, memberId);
    }

    public void SetStatus(CompanyMembershipStatus status)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
    }
}
