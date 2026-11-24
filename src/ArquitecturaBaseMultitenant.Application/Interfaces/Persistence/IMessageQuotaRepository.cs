using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Los contadores por hora de los topes de mensajes (ADR 0063), globales y sin RLS como <c>identity.WhatsAppCodeQuotas</c>.
/// Todo va dentro de la transacción del llamador: reservar y lo que se manda se guardan o se deshacen juntos.
/// </summary>
public interface IMessageQuotaRepository
{
    /// <summary>Toma el lock de cada contador hasta el commit, en un orden fijo: dos pedidos no se pasan por el borde.</summary>
    Task LockAsync(IReadOnlyCollection<MessageQuotaKey> keys, CancellationToken cancellationToken);

    /// <summary>Lo que sumó el contador en cada hora desde <paramref name="fromHourUtc"/>.</summary>
    Task<IReadOnlyList<MessageQuotaHour>> ListAsync(MessageQuotaKey key, DateTime fromHourUtc,
        CancellationToken cancellationToken);

    /// <summary>Suma uno al contador en la hora <paramref name="hourUtc"/>.</summary>
    Task IncrementAsync(MessageQuotaKey key, DateTime hourUtc, CancellationToken cancellationToken);
}

/// <summary>Un contador: qué cuenta y de quién, con la clave ya protegida por un HMAC.</summary>
public sealed record MessageQuotaKey(MessageQuotaScope Scope, string KeyHash)
{
    /// <summary>La clave del contador global, que no es de nadie.</summary>
    public const string GlobalKey = "all";

    public static MessageQuotaKey Global { get; } = new(MessageQuotaScope.Global, GlobalKey);
}
