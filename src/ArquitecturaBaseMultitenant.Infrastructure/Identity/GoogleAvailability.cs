using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>
/// Expone a Application si el ingreso con Google está habilitado por la configuración. Implementa el puerto
/// de disponibilidad sin obligar a los servicios a leer opciones técnicas.
/// </summary>
internal sealed record GoogleAvailability(bool IsEnabled) : IGoogleAvailability;
