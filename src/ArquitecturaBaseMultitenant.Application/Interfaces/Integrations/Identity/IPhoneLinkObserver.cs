using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Observa cambios de Phone en la UoW del núcleo; no se invoca al cambiar solo el principal.</summary>
public interface IPhoneLinkObserver
{
    Task VerifiedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct);
    Task RemovedAsync(Guid userId, Guid methodId, PhoneNumber phone, CancellationToken ct);
    Task AccountDeletedAsync(Guid userId, CancellationToken ct);
}
