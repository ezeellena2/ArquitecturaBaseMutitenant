namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;

/// <summary>Datos técnicos de la petición para evidencias de seguridad; nunca se registran en logs.</summary>
public interface IRequestInfo
{
    string? IpAddress { get; }

    string? UserAgent { get; }
}
