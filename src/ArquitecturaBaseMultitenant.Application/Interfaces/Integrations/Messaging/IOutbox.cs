namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>Encola el payload de un canal registrado dentro de la transacción del caso de uso.</summary>
public interface IOutbox
{
    void Enqueue(string channel, string payload, Guid? userId = null);
}
