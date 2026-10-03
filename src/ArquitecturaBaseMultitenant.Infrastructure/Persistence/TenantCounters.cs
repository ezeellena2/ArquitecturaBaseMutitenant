namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Agregados técnicos globales; los triggers privados mantienen una fila por espacio.</summary>
internal sealed class TenantCounters
{
    private TenantCounters() { }

    public Guid TenantId { get; private set; }
    public int CompanyCount { get; private set; }
    public int MemberCount { get; private set; }
}
