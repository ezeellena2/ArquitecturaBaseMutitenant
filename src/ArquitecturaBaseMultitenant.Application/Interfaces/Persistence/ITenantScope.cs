namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Entra temporalmente a un espacio después de autorizar, antes de abrir una transacción.</summary>
public interface ITenantScope
{
    IDisposable Enter(Guid tenantId);
}
