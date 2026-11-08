using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Users;

/// <summary>
/// Que una cuenta abrió un aviso de la campana (base de la 12). Los avisos no se guardan: salen de las fuentes de cada
/// área al leer, y solo queda cuál abrió cada cuenta. Es global y por cuenta, como sus métodos de ingreso; su clave es
/// la cuenta con el id del aviso. Se limpia a los <see cref="RetentionDays"/> días y se borra con la cuenta.
/// </summary>
public sealed class NotificationRead : Entity
{
    public const int RetentionDays = 90;

    private NotificationRead() { }

    public Guid UserId { get; private set; }
    public string NotificationId { get; private set; } = string.Empty;
    public DateTime ReadAtUtc { get; private set; }

    public static NotificationRead Mark(Guid userId, string notificationId, DateTime nowUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("The account is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);
        if (notificationId.Length > TextLimits.NotificationId)
            throw new ArgumentException("The notification id is too long.", nameof(notificationId));
        if (nowUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("The instant must be UTC.", nameof(nowUtc));
        return new NotificationRead { UserId = userId, NotificationId = notificationId, ReadAtUtc = nowUtc };
    }
}
