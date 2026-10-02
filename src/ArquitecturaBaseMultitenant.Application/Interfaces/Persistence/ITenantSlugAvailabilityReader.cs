namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>Comprueba reservas de plataforma y direcciones ya asignadas a organizaciones.</summary>
public interface ITenantSlugAvailabilityReader
{
    Task<bool> IsReservedAsync(string normalizedSlug, CancellationToken cancellationToken);

    Task<bool> IsTakenAsync(string normalizedSlug, CancellationToken cancellationToken);
}
