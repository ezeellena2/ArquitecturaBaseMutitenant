namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Habilitaciones efectivas del espacio activo para pantallas, workers y mensajes.</summary>
public interface IFeatureService
{
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListEnabledAsync(CancellationToken cancellationToken);
}
