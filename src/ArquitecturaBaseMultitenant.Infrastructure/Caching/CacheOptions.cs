using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Infrastructure.Caching;

/// <summary>
/// La conexión y los límites de la caché compartida (ADR 0052), validados al arrancar. Los timeouts cortos evitan que
/// una caída de Redis demore cada pedido más que una operación; la conexión sale de <c>ConnectionStrings:cache</c>.
/// </summary>
internal sealed class CacheOptions
{
    internal const string SectionName = "Cache";

    /// <summary>El ambiente del prefijo de las claves; por defecto, el del host.</summary>
    [Required]
    [RegularExpression("^[A-Za-z0-9-]{1,40}$")]
    public string Environment { get; set; } = string.Empty;

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(100, 10_000)]
    public int ConnectTimeoutMilliseconds { get; set; } = 1_000;

    [Range(50, 5_000)]
    public int OperationTimeoutMilliseconds { get; set; } = 500;

    /// <summary>El prefijo de toda clave y etiqueta: la aplicación y el ambiente, nunca un dato del pedido.</summary>
    internal string KeyPrefix => "arquitecturabasemt:" + Environment + ":";
}
