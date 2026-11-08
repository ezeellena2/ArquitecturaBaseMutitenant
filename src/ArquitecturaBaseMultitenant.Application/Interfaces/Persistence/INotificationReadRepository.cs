using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Guarda qué avisos de la campana abrió cada cuenta, dentro de la transacción del caso de uso.</summary>
public interface INotificationReadRepository
{
    void Add(NotificationRead read);
}
