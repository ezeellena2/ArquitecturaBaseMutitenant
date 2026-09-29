namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Origen público configurado para enlaces generados fuera de una petición.</summary>
public interface IPublicOrigin
{
    Uri? Value { get; }
}
