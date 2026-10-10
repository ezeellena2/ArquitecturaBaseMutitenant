using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
/// <summary>Admite un código que se va a encolar, dentro de la UoW del llamador y con su propósito y dueño del servidor.</summary>
public interface ILoginCodeAdmission
{
    string Channel { get; }
    Task<Result> ReserveAsync(LoginCodeDestination destination, LoginCodePurpose purpose, Guid? ownerId,
        CancellationToken cancellationToken);
}
