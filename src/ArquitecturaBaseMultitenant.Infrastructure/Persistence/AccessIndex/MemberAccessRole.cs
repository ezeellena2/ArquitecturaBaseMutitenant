namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex;

/// <summary>Rol sistémico reservado por miembro; sólo los triggers lo leen y mantienen, sin privilegios runtime.</summary>
internal sealed class MemberAccessRole
{
    private MemberAccessRole() { }
    public Guid TenantId { get; private set; }
    public Guid MemberId { get; private set; }
    public string RoleName { get; private set; } = string.Empty;
}
