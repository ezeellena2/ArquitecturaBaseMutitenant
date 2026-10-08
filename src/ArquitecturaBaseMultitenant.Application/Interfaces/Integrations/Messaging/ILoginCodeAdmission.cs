using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
/// <summary>Reserva capacidad del canal dentro de la UoW del emisor, después de sus límites por destino.</summary>
public interface ILoginCodeAdmission
{
    string Channel { get; }
    Task<Result> ReserveAsync(LoginCodeDestination destination, CancellationToken cancellationToken);
}
