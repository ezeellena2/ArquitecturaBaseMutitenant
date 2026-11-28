using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;

/// <summary>
/// Admite una invitación que se va a encolar por su canal (<see cref="Channel"/>, la clave de <see cref="IInvitationChannel"/>):
/// los topes diarios de ese canal, dentro de la UoW del llamador y después de sus locks. El núcleo trae la del correo (ADR 0063)
/// y un módulo registra la de su canal, como WhatsApp la de «Cargar teléfonos» (B9). Un canal sin admisión no tiene tope propio.
/// Pasado el tope devuelve el error con la espera en <c>retryAfter</c>, y no se guarda ni se manda nada.
/// </summary>
public interface IInvitationAdmission
{
    string Channel { get; }

    Task<Result> ReserveAsync(Guid inviterId, Guid organizationId, CancellationToken cancellationToken);
}
