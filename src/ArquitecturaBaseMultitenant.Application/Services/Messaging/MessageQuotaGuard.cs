using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Application.Services.Messaging;

/// <summary>
/// Los contadores por hora de los topes de mensajes (ADR 0063), en la transacción del llamador. Mirar toma el lock de cada
/// contador hasta el commit, en un orden fijo, y lee su ventana de 24 horas móviles; sumar va recién cuando todo lo demás
/// pasó, así un pedido rechazado no gasta lugar en ninguno. Lo usan <see cref="EmailQuotaGuard"/> y el tope por cuenta de
/// los códigos por WhatsApp. Llamalo después de los demás locks del caso: los suyos son los últimos.
/// </summary>
internal sealed class MessageQuotaGuard(IMessageQuotaRepository buckets, TimeProvider clock)
{
    /// <summary>
    /// Lo que cuenta cada tope ahora, o el tope lleno que libera lugar más tarde, con su espera: con dos llenos a la vez, la
    /// espera del primero promete un lugar que el otro todavía no da. No suma nada: eso lo hace <see cref="RecordAsync"/>,
    /// en la misma transacción.
    /// </summary>
    internal async Task<MessageQuotaCheck> CheckAsync(IReadOnlyList<MessageQuotaLimit> limits, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(limits);
        await buckets.LockAsync([.. limits.Select(limit => limit.Key)], ct);
        // La hora se lee después del lock: la espera de otro pedido no corre la ventana.
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var counts = new int[limits.Count];
        MessageQuotaLimit? exceeded = null;
        var retryAfterSeconds = 0;
        for (var index = 0; index < limits.Count; index++)
        {
            var limit = limits[index];
            var hours = await buckets.ListAsync(limit.Key, MessageQuotaWindow.FirstHour(nowUtc), ct);
            counts[index] = MessageQuotaWindow.Count(hours, nowUtc);
            if (MessageQuotaWindow.WaitSeconds(hours, limit.Limit, nowUtc) is { } wait && wait > retryAfterSeconds)
                (exceeded, retryAfterSeconds) = (limit, wait);
        }
        return new MessageQuotaCheck(exceeded, retryAfterSeconds, counts, limits, nowUtc);
    }

    /// <summary>
    /// Suma uno a cada contador que cuenta este pedido, en la hora del control. Sus locks ya los tomó <see cref="CheckAsync"/>.
    /// </summary>
    internal async Task RecordAsync(MessageQuotaCheck check, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(check);
        if (check.Exceeded is not null) throw new InvalidOperationException("A full quota does not record.");
        var hourUtc = MessageQuotaWindow.HourOf(check.CheckedAtUtc);
        foreach (var limit in check.Limits.Where(limit => limit.Records)) await buckets.IncrementAsync(limit.Key, hourUtc, ct);
    }
}

/// <summary>
/// Un tope: el contador y cuántos entran en sus 24 horas móviles. Sin <paramref name="Records"/> se mira y no se suma: el
/// pedido responde lo mismo que uno que sí cuenta en él, sin gastarle lugar.
/// </summary>
internal sealed record MessageQuotaLimit(MessageQuotaKey Key, int Limit, bool Records = true);

/// <summary>
/// Lo que vio <see cref="MessageQuotaGuard.CheckAsync"/>: el tope lleno que libera lugar más tarde con los segundos de
/// espera, o ninguno; lo que contaba cada tope antes de este pedido y los topes, en el orden pedido. <see cref="MessageQuotaGuard.RecordAsync"/>
/// suma los que cuentan.
/// </summary>
internal sealed record MessageQuotaCheck(MessageQuotaLimit? Exceeded, int RetryAfterSeconds, IReadOnlyList<int> Counts,
    IReadOnlyList<MessageQuotaLimit> Limits, DateTime CheckedAtUtc);
