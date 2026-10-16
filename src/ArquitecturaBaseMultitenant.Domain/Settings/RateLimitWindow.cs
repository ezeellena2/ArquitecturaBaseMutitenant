namespace ArquitecturaBaseMultitenant.Domain.Settings;

/// <summary>
/// La ventana fija UTC de una cuota que contiene un instante: empieza en un múltiplo de su duración contado desde la época
/// Unix, así todas las réplicas cuentan en la misma ventana.
/// </summary>
public readonly record struct RateLimitWindow(DateTime StartUtc, DateTime EndUtc)
{
    public static RateLimitWindow Of(DateTime nowUtc, RateLimitQuota quota)
    {
        ArgumentNullException.ThrowIfNull(quota);
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("A UTC instant is required.", nameof(nowUtc));
        var length = TimeSpan.FromSeconds(quota.WindowSeconds).Ticks;
        var startUtc = DateTime.UnixEpoch.AddTicks((nowUtc - DateTime.UnixEpoch).Ticks / length * length);
        return new RateLimitWindow(startUtc, startUtc.AddTicks(length));
    }

    /// <summary>Los segundos enteros que faltan para la próxima ventana, al menos uno: el Retry-After de un rechazo.</summary>
    public int SecondsLeft(DateTime nowUtc) =>
        (int)Math.Clamp(Math.Ceiling((EndUtc - nowUtc).TotalSeconds), 1, int.MaxValue);
}
