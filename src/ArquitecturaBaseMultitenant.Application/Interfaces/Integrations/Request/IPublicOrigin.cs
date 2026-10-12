namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Origen público configurado para enlaces generados fuera de una petición.</summary>
public interface IPublicOrigin
{
    Uri? Value { get; }

    /// <summary>Compone un destino desde el origen configurado; sin origen conserva el path relativo.</summary>
    string Absolute(string path) => Value is { } origin ? new Uri(origin, path).AbsoluteUri : path;
}
