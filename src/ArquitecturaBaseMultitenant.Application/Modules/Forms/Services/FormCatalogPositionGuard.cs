using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;
using ArquitecturaBaseMultitenant.Domain.Forms;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>
/// Protege las reglas de las opciones de un catálogo a mano bajo su bloqueo, que ya tomó el repositorio al leerlo (C4): el tope de
/// 10.000 y el código único sin distinguir mayúsculas. Solo cuenta, relee y devuelve el error: las posiciones las acomoda
/// <see cref="FormCatalogPositionEditor"/>. Las dos restricciones diferidas de la base quedan de respaldo: un choque en el commit
/// sería un 500.
/// </summary>
internal sealed class FormCatalogPositionGuard(IFormCatalogRepository repository)
{
    /// <summary>El tope de opciones, activas y dadas de baja, sin las que se borran en este guardado (§3).</summary>
    public async Task<Result> CheckCapacityAsync(FormCatalog catalog, int removed, int created, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (created == 0) return Result.Success();
        var count = await repository.CountItemsAsync(catalog.Id, ct);
        return count - removed + created > FormCatalogLimits.MaxItems ? FormCatalogErrors.TooManyItems : Result.Success();
    }

    /// <summary>
    /// El código de cada opción nueva o cambiada tiene que estar libre: no lo puede tener otra que queda, ni otra del mismo pedido.
    /// Lee solo las que tienen guardado alguno de esos códigos; una que se borra lo libera, y dos del pedido pueden intercambiarlos.
    /// El error dice el número de la opción que ya lo tiene, con las posiciones ya acomodadas.
    /// </summary>
    /// <param name="catalog">El catálogo, ya bloqueado.</param>
    /// <param name="changed">Las opciones nuevas o cambiadas, con su índice en el pedido, en el orden del pedido.</param>
    /// <param name="removed">Las que se borran en este guardado, que liberan su código.</param>
    /// <param name="ct">El token del pedido.</param>
    public async Task<Result> CheckCodesAsync(FormCatalog catalog, IReadOnlyList<(int Index, FormCatalogItem Item)> changed,
        IReadOnlyCollection<FormCatalogItem> removed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(removed);
        if (changed.Count == 0) return Result.Success();
        var codes = changed.Select(entry => entry.Item.CodeNormalized).ToHashSet(StringComparer.Ordinal);
        var released = changed.Select(entry => entry.Item.Id).Concat(removed.Select(item => item.Id)).ToHashSet();
        var taken = (await repository.ListItemsByCodesAsync(catalog.Id, codes, ct))
            .Where(holder => !released.Contains(holder.Id))
            .ToDictionary(holder => holder.CodeNormalized, StringComparer.Ordinal);
        foreach (var (index, item) in changed)
        {
            if (taken.TryGetValue(item.CodeNormalized, out var other)) return FormCatalogErrors.DuplicateCode(index, other.Position + 1);
            taken[item.CodeNormalized] = item;
        }

        return Result.Success();
    }
}
