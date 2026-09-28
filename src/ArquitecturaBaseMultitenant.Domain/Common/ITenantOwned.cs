namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Dato privado de un espacio personal u organización.</summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
