namespace ArquitecturaBaseMultitenant.Domain.Platform;

/// <summary>Estado de un pedido de «Recuperar mi cuenta»: lo resuelve un operador de la plataforma.</summary>
public enum AccountRecoveryStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}
