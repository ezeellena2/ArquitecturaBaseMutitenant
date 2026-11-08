using System.ComponentModel.DataAnnotations;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Notifications;

/// <summary>
/// La campana de quien mira, en los tres accesos: hasta 50 avisos, del más nuevo al más viejo. Los avisos se arman al leer,
/// desde las fuentes de cada área; solo se guarda cuáles abrió cada persona. <c>Unread</c> es el número de la campana:
/// cuántos de <c>Items</c> están sin leer.
/// </summary>
public sealed record NotificationListResponse(
    [property: MaxLength(NotificationListResponse.Limit)] IReadOnlyList<NotificationItem> Items, int Unread)
{
    public const int Limit = 50;
}

/// <summary>
/// Un aviso. <c>Id</c> es estable y sale de la fuente: si el aviso cambia («3 por aprobar» pasa a 4), cambia de id y vuelve a
/// contar. <c>Kind</c> es una clave abierta que suma cada área; el front arma el texto con <c>Kind</c> y <c>Params</c>, en el
/// idioma de quien mira. <c>Link</c> es la ruta del front donde se resuelve. La fuente lo entrega sin leer; el servicio marca
/// <c>Read</c>.
/// </summary>
public sealed record NotificationItem([property: MaxLength(NotificationItem.IdMaxLength)] string Id, string Kind,
    IReadOnlyDictionary<string, string> Params, DateTime OccurredAtUtc, string Link, bool Read)
{
    public const int IdMaxLength = TextLimits.NotificationId;
}

/// <summary>
/// Quién mira la campana, calculado una sola vez por pedido: su cuenta, su acceso y los permisos de ese acceso (los de la
/// organización en Business, con sus proyecciones; los de Plataforma en Platform; ninguno en Consumer, que tiene implícitos
/// los <c>personal.*</c>). Con Business, también los de cada empresa donde tiene roles. Copia el alcance de
/// <see cref="Home.PendingItemContext"/>.
/// </summary>
public sealed record NotificationContext(Guid UserId, Access Access, IReadOnlyCollection<string> Permissions,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>>? CompanyPermissions = null)
{
    public bool Can(string permission) => Permissions.Contains(permission, StringComparer.Ordinal);

    /// <summary>Las empresas donde tiene el permiso, siempre en el mismo orden.</summary>
    public IReadOnlyList<Guid> CompaniesWhere(string permission) =>
    [
        .. (CompanyPermissions ?? new Dictionary<Guid, IReadOnlyList<string>>())
            .Where(company => company.Value.Contains(permission, StringComparer.Ordinal))
            .Select(company => company.Key)
            .Order(),
    ];
}
