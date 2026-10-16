using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Domain.Settings;

namespace ArquitecturaBaseMultitenant.Application.Models.Settings;

/// <summary>
/// Un pedido a admitir (E9): los perfiles que le tocan, la IP real (la de los proxies confiables), la identidad firmada y
/// la organización del claim, si las hay. El servicio arma con ellas las particiones y nunca guarda la IP completa.
/// </summary>
public sealed record RateLimitRequest(IReadOnlyList<RateLimitProfile> Profiles, string? IpAddress, Guid? UserId,
    Guid? TenantId)
{
    public override string ToString() => nameof(RateLimitRequest);
}

/// <summary>Una cuota aplicada a un pedido: su perfil, la partición sin datos personales, la cuota vigente y su ventana.</summary>
public sealed record RateLimitBucket(RateLimitProfile Profile, string Partition, RateLimitQuota Quota,
    RateLimitWindow Window)
{
    /// <summary>
    /// La clave del contador, la misma en Redis y en PostgreSQL: perfil, duración e inicio de la ventana y partición. Otra
    /// duración es otra cuenta; otro tope con la misma ventana sigue la misma.
    /// </summary>
    public string Key => string.Join(':', RateLimitProfiles.Name(Profile), VersionWireFormatter.Format((long)Quota.WindowSeconds),
        VersionWireFormatter.Format((long)(Window.StartUtc - DateTime.UnixEpoch).TotalSeconds), Partition);
}

/// <summary>Lo que respondió un contador: si admitió el pedido y cuánto lleva cada cuota, en el orden de las cuotas.</summary>
public sealed record RateLimitTally(bool Admitted, IReadOnlyList<int> Counts);
