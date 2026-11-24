using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
/// <summary>Admite un código que se va a encolar, dentro de la UoW del llamador y con su propósito y dueño del servidor.</summary>
public interface ILoginCodeAdmission
{
    string Channel { get; }
    Task<Result> ReserveAsync(LoginCodeDestination destination, LoginCodePurpose purpose, Guid? ownerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Un código que se emite y no sale (una cuenta bloqueada, suspendida o sin ese método): el canal cuenta y rechaza lo que
    /// haga falta para no decir por qué no salió (ADR 0063), con el propósito y el dueño de <see cref="ReserveAsync"/>. Por
    /// defecto no cuenta nada.
    /// </summary>
    Task<Result> ReserveWithheldAsync(LoginCodeDestination destination, LoginCodePurpose purpose, Guid? ownerId,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
