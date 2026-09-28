using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Isolation;

internal sealed class Widget : Entity, ITenantOwned, IAuditable, ISoftDeletable, IVersioned
{
    private Widget()
    {
    }

    public Widget(Guid tenantId, string name)
    {
        TenantId = tenantId;
        Name = name;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Email? Email { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }
    public uint Version { get; private set; }
}
