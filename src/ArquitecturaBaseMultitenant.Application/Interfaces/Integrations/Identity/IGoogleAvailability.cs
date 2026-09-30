namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;

/// <summary>Indica a los casos de uso si el ingreso con Google está configurado antes de ofrecerlo como opción.</summary>
public interface IGoogleAvailability
{
    bool IsEnabled { get; }
}
