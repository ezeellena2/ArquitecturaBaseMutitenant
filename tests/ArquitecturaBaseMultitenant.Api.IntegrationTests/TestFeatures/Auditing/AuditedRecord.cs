using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.Auditing;

internal sealed class AuditedRecord : Entity, ITenantOwned, IAuditable, ISoftDeletable
{
    private AuditedRecord()
    {
    }

    public AuditedRecord(Guid tenantId, string name, string secret, string contactEmail)
    {
        TenantId = tenantId;
        Name = name;
        Secret = secret;
        ContactEmail = contactEmail;
    }

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    [NotAudited] public string Secret { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAtUtc { get; private set; }
    public Guid? DeletedBy { get; private set; }

    public void Rename(string name) => Name = name;

    public void ChangeSecret(string secret) => Secret = secret;
}

internal sealed class AuditedPublicRecord(Guid businessTenantId) : Entity, IPublishedByBusiness, IAuditable
{
    public Guid BusinessTenantId { get; private set; } = businessTenantId;
    public bool IsPublished { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
}

internal sealed class AuditedSharedRecord(Guid consumerTenantId, Guid businessTenantId)
    : Entity, IConsumerBusinessShared, IAuditable
{
    public Guid ConsumerTenantId { get; private set; } = consumerTenantId;
    public Guid BusinessTenantId { get; private set; } = businessTenantId;
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
}
