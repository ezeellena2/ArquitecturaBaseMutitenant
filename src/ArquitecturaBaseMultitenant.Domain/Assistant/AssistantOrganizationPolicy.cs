using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Auditing;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>
/// Lo que Plataforma decide sobre el Asistente de una organización: si puede usar su propia clave. Los límites y el
/// presupuesto viven en la configuración de cada organización y en la de Plataforma (E9 quitó los campos muertos).
/// </summary>
public sealed class AssistantOrganizationPolicy : Entity, IGlobalSecurityAudited, IVersioned
{
    private AssistantOrganizationPolicy() { }
    SecurityEventType IGlobalSecurityAudited.SecurityEventType => SecurityEventType.TenantFeaturesChanged;
    Guid? IGlobalSecurityAudited.SecurityEventTargetTenantId => OrganizationId;
    public uint Version { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime? ModifiedAtUtc { get; private set; }
    public Guid? ModifiedBy { get; private set; }
    public Guid OrganizationId { get; private set; }
    public bool AllowOwnKey { get; private set; }
    public void SetAllowOwnKey(bool allowOwnKey) => AllowOwnKey = allowOwnKey;

    public static AssistantOrganizationPolicy Create(Guid organizationId)
    {
        if (organizationId == Guid.Empty) throw new ArgumentException("An organization is required.", nameof(organizationId));
        return new AssistantOrganizationPolicy { OrganizationId = organizationId };
    }
}
