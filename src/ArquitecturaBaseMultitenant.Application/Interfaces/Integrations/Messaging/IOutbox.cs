namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>
/// Encola el payload de un canal registrado dentro de la transacción del caso de uso. Devuelve el identificador del mensaje,
/// con el que un módulo une después lo que informa el proveedor.
/// </summary>
public interface IOutbox
{
    Guid Enqueue(string channel, string payload, Guid? userId = null);
}
