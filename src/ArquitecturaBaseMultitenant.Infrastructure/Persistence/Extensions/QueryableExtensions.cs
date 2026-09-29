using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

public static class QueryableExtensions
{
    private static readonly MethodInfo LowerMethod = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    private static readonly MethodInfo UnaccentMethod = typeof(SearchFunctions).GetMethod(nameof(SearchFunctions.Unaccent))!;
    private static readonly MethodInfo LikeMethod = typeof(DbFunctionsExtensions).GetMethod(nameof(DbFunctionsExtensions.Like),
        [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;
    private static readonly MethodInfo RowLessThanMethod = typeof(NpgsqlDbFunctionsExtensions)
        .GetMethod(nameof(NpgsqlDbFunctionsExtensions.LessThan),
            [typeof(DbFunctions), typeof(ITuple), typeof(ITuple)])!;
    private static readonly ConstructorInfo CursorRowConstructor = typeof(ValueTuple<DateTime, Guid>)
        .GetConstructor([typeof(DateTime), typeof(Guid)])!;

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

    public static IQueryable<T> ApplySearch<T>(
        this IQueryable<T> query,
        string? search,
        params Expression<Func<T, string?>>[] searchableFields)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(searchableFields);
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        if (searchableFields.Length == 0)
        {
            throw new ArgumentException("At least one searchable field is required.", nameof(searchableFields));
        }

        var parameter = Expression.Parameter(typeof(T), "entry");
        var pattern = Expression.Constant($"%{EscapeLike(search.Trim())}%");
        var normalizedPattern = Expression.Call(UnaccentMethod, Expression.Call(pattern, LowerMethod));
        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        Expression? predicate = null;

        foreach (var field in searchableFields)
        {
            ArgumentNullException.ThrowIfNull(field);
            var column = new ReplaceParameterVisitor(field.Parameters[0], parameter).Visit(field.Body)!;
            var normalizedColumn = Expression.Call(UnaccentMethod, Expression.Call(column, LowerMethod));
            var match = Expression.Call(LikeMethod, efFunctions, normalizedColumn,
                normalizedPattern, Expression.Constant("\\"));
            predicate = predicate is null ? match : Expression.OrElse(predicate, match);
        }

        return query.Where(Expression.Lambda<Func<T, bool>>(predicate!, parameter));
    }

    public static Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Page, int.MaxValue / request.PageSize);
        return CountAndPageAsync(query, request, cancellationToken);
    }

    private static async Task<PagedResult<T>> CountAndPageAsync<T>(
        IQueryable<T> query, PagedRequest request, CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize).ToListAsync(cancellationToken);
        return new PagedResult<T>(items, request.Page, request.PageSize, totalCount);
    }

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    private sealed class ReplaceParameterVisitor(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }

    public static async Task<CursorResult<T>> ToCursorResultAsync<T>(
        this IQueryable<T> query,
        CursorRequest request,
        Expression<Func<T, DateTime>> sortUtc,
        Expression<Func<T, Guid>> id,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sortUtc);
        ArgumentNullException.ThrowIfNull(id);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.Limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(request.Limit, CursorRequest.MaxLimit);

        if (request.After is { } after)
        {
            if (!CursorCodec.TryDecode(after, out var position))
            {
                throw new InvalidCursorException();
            }

            var parameter = Expression.Parameter(typeof(T), "entry");
            var sortValue = new ReplaceParameterVisitor(sortUtc.Parameters[0], parameter).Visit(sortUtc.Body)!;
            var idValue = new ReplaceParameterVisitor(id.Parameters[0], parameter).Visit(id.Body)!;
            var row = Expression.Convert(Expression.New(CursorRowConstructor, sortValue, idValue), typeof(ITuple));
            var cursor = Expression.Convert(Expression.Constant(ValueTuple.Create(position.SortUtc, position.Id)), typeof(ITuple));
            var predicate = Expression.Call(RowLessThanMethod,
                Expression.Property(null, typeof(EF), nameof(EF.Functions)), row, cursor);
            query = query.Where(Expression.Lambda<Func<T, bool>>(predicate, parameter));
        }

        var page = await query.OrderByDescending(sortUtc).ThenByDescending(id)
            .Take(request.Limit + 1).ToListAsync(cancellationToken);
        var hasMore = page.Count > request.Limit;
        if (hasMore)
        {
            page.RemoveAt(page.Count - 1);
        }

        var nextCursor = hasMore
            ? CursorCodec.Encode(sortUtc.Compile()(page[^1]), id.Compile()(page[^1]))
            : null;
        return new CursorResult<T>(page, nextCursor, hasMore);
    }
}
