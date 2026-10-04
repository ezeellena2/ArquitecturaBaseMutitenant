namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>
/// Contacto del Dueño invitado al crear una organización desde Plataforma, mientras su invitación sigue
/// pendiente. Es una copia técnica global: la lista de Plataforma no lee Invitations fuera de RLS. Un trigger
/// la borra cuando la invitación se acepta, se revoca o se elimina, así no queda un correo huérfano.
/// </summary>
internal sealed class PendingTenantOwner
{
    private PendingTenantOwner()
    {
        Address = string.Empty;
    }

    internal PendingTenantOwner(Guid invitationId, Guid tenantId, string address)
    {
        InvitationId = invitationId;
        TenantId = tenantId;
        Address = address;
    }

    public Guid InvitationId { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Dirección normalizada del <c>Email</c> invitado; se guarda como texto para buscar en SQL.</summary>
    public string Address { get; private set; }
}
