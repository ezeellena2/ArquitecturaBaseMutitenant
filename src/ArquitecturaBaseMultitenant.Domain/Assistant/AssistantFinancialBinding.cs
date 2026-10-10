using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>Binding técnico previamente autorizado por el servidor. Nunca se admite desde HTTP.</summary>
public sealed record AssistantFinancialBinding(Guid UserId, Access Access, Guid? ScopeTenantId)
{
    public void Validate()
    {
        if (UserId == Guid.Empty || !Enum.IsDefined(Access)
            || (Access == Access.Platform ? ScopeTenantId is not null : ScopeTenantId is null || ScopeTenantId == Guid.Empty))
            throw new ArgumentException("A complete server binding is required.");
    }
}
