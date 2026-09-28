using System.Linq.Expressions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Ordena por un campo de la lista blanca (sin distinguir mayúsculas) y siempre desempata en forma
    /// ascendente con <paramref name="tieBreaker"/>, que debe ser una clave única (normalmente el Id):
    /// sin desempate, dos páginas consecutivas (cada una un LIMIT/OFFSET independiente) pueden repetir o
    /// saltear filas cuando el campo de orden tiene valores repetidos. Sin orden pedido, usa
    /// <paramref name="defaultSort"/>. Un campo fuera de la lista es un error de programación: el validador
    /// de la consulta ya tuvo que rechazarlo.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        SortDescriptor? sort,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> sortableFields,
        SortDescriptor defaultSort,
        Expression<Func<T, object?>> tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(sortableFields);
        ArgumentNullException.ThrowIfNull(defaultSort);
        ArgumentNullException.ThrowIfNull(tieBreaker);

        var effectiveSort = sort ?? defaultSort;

        var keySelector = sortableFields
            .FirstOrDefault(field => string.Equals(field.Key, effectiveSort.Field, StringComparison.OrdinalIgnoreCase))
            .Value
            ?? throw new InvalidOperationException($"'{effectiveSort.Field}' is not in the sort whitelist.");

        var ordered = effectiveSort.Descending ? query.OrderByDescending(keySelector) : query.OrderBy(keySelector);

        return ordered.ThenBy(tieBreaker);
    }

}
