namespace ArquitecturaBaseMultitenant.Domain.Messaging;

/// <summary>
/// Las 24 horas móviles de los topes de mensajes (ADR 0063): se cuentan por horas, sumando las 24 horas cerradas y la
/// actual, así la espera tiene una hora de precisión. Una hora sale de la ventana 25 horas después de empezar, y la
/// purga borra lo que ya salió.
/// </summary>
public static class MessageQuotaWindow
{
    /// <summary>Cuántas horas cerradas suma la ventana, además de la actual.</summary>
    public const int ClosedHours = 24;

    private static readonly TimeSpan Span = TimeSpan.FromHours(ClosedHours + 1);

    /// <summary>La hora en que cae <paramref name="nowUtc"/>: el contador que suma lo que pasa ahora.</summary>
    public static DateTime HourOf(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        return new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, nowUtc.Hour, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>La hora más vieja que todavía cuenta.</summary>
    public static DateTime FirstHour(DateTime nowUtc) => HourOf(nowUtc).AddHours(-ClosedHours);

    /// <summary>Lo anterior a este momento ya salió de toda ventana: la purga lo borra.</summary>
    public static DateTime PurgeBefore(DateTime nowUtc)
    {
        RequireUtc(nowUtc);
        return nowUtc - Span;
    }

    /// <summary>
    /// Null si la ventana todavía tiene lugar para <paramref name="amount"/> más bajo <paramref name="limit"/>; si no, los
    /// segundos hasta que salen de la ventana las horas más viejas que hacen falta para dejar lugar (al menos 1). Si el lote
    /// supera el tope diario entero, espera la ventana completa.
    /// </summary>
    public static int? WaitSeconds(IReadOnlyCollection<MessageQuotaHour> hours, int limit, DateTime nowUtc, int amount = 1)
    {
        ArgumentNullException.ThrowIfNull(hours);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        var first = FirstHour(nowUtc);
        var counted = hours.Where(hour => hour.HourUtc >= first && hour.HourUtc <= nowUtc).OrderBy(hour => hour.HourUtc).ToArray();
        long total = counted.Sum(hour => (long)hour.Count);
        if (total + amount <= limit) return null;
        if (amount > limit) return Math.Max(1, (int)Math.Ceiling((HourOf(nowUtc) + Span - nowUtc).TotalSeconds));
        foreach (var hour in counted)
        {
            total -= hour.Count;
            if (total + amount <= limit) return Math.Max(1, (int)Math.Ceiling((hour.HourUtc + Span - nowUtc).TotalSeconds));
        }
        throw new InvalidOperationException("A full window always frees a place when its hours leave it.");
    }

    /// <summary>Lo que cuenta la ventana en este momento.</summary>
    public static int Count(IReadOnlyCollection<MessageQuotaHour> hours, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(hours);
        var first = FirstHour(nowUtc);
        return hours.Where(hour => hour.HourUtc >= first && hour.HourUtc <= nowUtc).Sum(hour => hour.Count);
    }

    private static void RequireUtc(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The window runs on UTC time.", nameof(nowUtc));
    }
}

/// <summary>Lo que sumó un contador en una hora.</summary>
public readonly record struct MessageQuotaHour(DateTime HourUtc, int Count);
