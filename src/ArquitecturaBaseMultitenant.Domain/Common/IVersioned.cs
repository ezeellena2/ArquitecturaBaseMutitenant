namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Versión de concurrencia que Infrastructure mapea a xmin.</summary>
public interface IVersioned
{
    uint Version { get; }
}
