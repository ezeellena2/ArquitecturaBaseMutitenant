using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex;

/// <summary>Proyección técnica global de membresías; la tabla tenant.Members es la fuente.</summary>
internal sealed class UserTenantAccess
{
    private UserTenantAccess() { }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public MemberStatus Status { get; private set; }

    public DateTime? JoinedAtUtc { get; private set; }
}
