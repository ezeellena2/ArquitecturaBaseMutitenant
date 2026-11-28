using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>Construye orden, búsqueda y paginación con expresiones EF traducibles a PostgreSQL. Solo acepta campos declarados por el reader y mantiene un desempate estable entre páginas.</summary>
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
    private static readonly ConcurrentDictionary<MemberKey, Delegate> MemberAccessors = new();

    /// <summary>
    /// Ordena por un campo de la lista blanca (sin distinguir mayúsculas) con la regla de todas las tablas (decisión 264):
    /// lo vacío va al final en los dos sentidos; un enum (un estado, un tipo) ordena por el orden en que se declaró, el de
    /// su circuito, también si se guarda como texto, y nunca por su texto; un campo con varias claves se declara con un
    /// tipo anónimo (<c>row =&gt; new { row.Prefix, row.Number }</c>) y queda vacío si le falta alguna. Con el mismo valor,
    /// las filas siguen el orden de siempre de la lista (<paramref name="defaultSort"/>) y al final
    /// <paramref name="tieBreaker"/>, que debe ser una clave única (normalmente el Id): sin desempate, dos páginas
    /// consecutivas (cada una un LIMIT/OFFSET independiente) pueden repetir o saltear filas. Sin orden pedido, usa
    /// <paramref name="defaultSort"/>. Un campo fuera de la lista es un error de programación: el validador de la consulta
    /// ya tuvo que rechazarlo.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        SortDescriptor? sort,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> sortableFields,
        SortDescriptor defaultSort,
        Expression<Func<T, object?>> tieBreaker)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(sortableFields);
        ArgumentNullException.ThrowIfNull(defaultSort);
        ArgumentNullException.ThrowIfNull(tieBreaker);

        var usual = new SortKey<T>(Field(sortableFields, defaultSort.Field), defaultSort.Descending);
        // Pedir el campo del orden de siempre no lo repite: manda el sentido pedido.
        SortKey<T>[] keys = sort is null || string.Equals(sort.Field, defaultSort.Field, StringComparison.OrdinalIgnoreCase)
            ? [sort is null ? usual : usual with { Descending = sort.Descending }]
            : [new SortKey<T>(Field(sortableFields, sort.Field), sort.Descending), usual];
        // PostgreSQL ya deja NULL al final en un ASC: ahí el «es nulo» solo hace falta en un DESC o en un campo con varias
        // claves, y repetiría la clave, que puede ser una subconsulta. En memoria (los tests) siempre va.
        var nullsLastWhenAscending = query.Provider is IAsyncQueryProvider;
        IOrderedQueryable<T>? ordered = null;
        foreach (var key in keys)
        {
            ordered = OrderBy(query, ordered, key, nullsLastWhenAscending);
        }

        return ordered!.ThenBy(tieBreaker);
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
        var rawPattern = $"%{EscapeLike(search.Trim())}%";
        Expression<Func<string>> patternAccessor = () => rawPattern;
        var normalizedPattern = Expression.Call(UnaccentMethod, Expression.Call(patternAccessor.Body, LowerMethod));
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

    /// <summary>Escapa los comodines de LIKE con la barra invertida, el escape de <c>ApplySearch</c>.</summary>
    internal static string EscapeLike(string value) => value
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
            var cursorSortUtc = position.SortUtc;
            var cursorId = position.Id;
            Expression<Func<ITuple>> cursorAccessor = () => ValueTuple.Create(cursorSortUtc, cursorId);
            var predicate = Expression.Call(RowLessThanMethod,
                Expression.Property(null, typeof(EF), nameof(EF.Functions)), row, cursorAccessor.Body);
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
            ? CursorCodec.Encode(Accessor(sortUtc)(page[^1]), Accessor(id)(page[^1]))
            : null;
        return new CursorResult<T>(page, nextCursor, hasMore);
    }

    private static Func<T, TResult> Accessor<T, TResult>(Expression<Func<T, TResult>> selector)
    {
        if (selector.Body is MemberExpression { Expression: ParameterExpression parameter } member
            && parameter == selector.Parameters[0])
        {
            return (Func<T, TResult>)MemberAccessors.GetOrAdd(
                new MemberKey(typeof(T), typeof(TResult), member.Member),
                static (_, expression) => expression.Compile(),
                selector);
        }

        return selector.Compile();
    }

    private static Expression<Func<T, object?>> Field<T>(IReadOnlyDictionary<string, Expression<Func<T, object?>>> sortableFields,
        string field) =>
        sortableFields.FirstOrDefault(candidate => string.Equals(candidate.Key, field, StringComparison.OrdinalIgnoreCase)).Value
        ?? throw new InvalidOperationException($"'{field}' is not in the sort whitelist.");

    // Un campo puede tener varias claves (un tipo anónimo). Está vacío si le falta alguna, y lo vacío va al final en los dos
    // sentidos: PostgreSQL solo pondría NULL primero en un DESC.
    private static IOrderedQueryable<T> OrderBy<T>(IQueryable<T> query, IOrderedQueryable<T>? ordered, SortKey<T> key,
        bool nullsLastWhenAscending)
    {
        var parameter = key.Key.Parameters[0];
        var parts = Parts(key.Key.Body);
        var empty = parts.Where(CanBeEmpty)
            .Select(part => (Expression)Expression.Equal(part, Expression.Constant(null, part.Type)))
            .Aggregate((Expression?)null, (all, next) => all is null ? next : Expression.OrElse(all, next));
        if (empty is not null && (key.Descending || parts.Length > 1 || !nullsLastWhenAscending))
        {
            ordered = Then(query, ordered, Lambda<T>(empty, parameter), descending: false);
        }

        foreach (var part in parts)
        {
            ordered = Then(query, ordered, Lambda<T>(Ordinal(part), parameter), key.Descending);
        }

        return ordered ?? throw new InvalidOperationException("A sort key has no parts.");
    }

    private static IOrderedQueryable<T> Then<T>(IQueryable<T> query, IOrderedQueryable<T>? ordered,
        Expression<Func<T, object?>> key, bool descending) =>
        ordered is null
            ? descending ? query.OrderByDescending(key) : query.OrderBy(key)
            : descending ? ordered.ThenByDescending(key) : ordered.ThenBy(key);

    private static Expression[] Parts(Expression body)
    {
        var unwrapped = body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert
            ? convert.Operand
            : body;

        return unwrapped is NewExpression { Members: not null } anonymous ? [.. anonymous.Arguments] : [unwrapped];
    }

    private static bool CanBeEmpty(Expression part) => !part.Type.IsValueType || Nullable.GetUnderlyingType(part.Type) is not null;

    // Un enum ordena por el orden en que se declaró (el de su circuito) con un CASE: guardado como texto, la columna sola
    // ordenaría sus nombres en inglés por el abecedario.
    private static Expression Ordinal(Expression part)
    {
        var type = Nullable.GetUnderlyingType(part.Type) ?? part.Type;
        if (!type.IsEnum)
        {
            return part;
        }

        var values = Enum.GetValues(type);
        Expression ordinal = Expression.Constant(values.Length);
        for (var index = values.Length - 1; index >= 0; index--)
        {
            ordinal = Expression.Condition(Expression.Equal(part, Expression.Constant(values.GetValue(index), part.Type)),
                Expression.Constant(index), ordinal);
        }

        return ordinal;
    }

    private static Expression<Func<T, object?>> Lambda<T>(Expression body, ParameterExpression parameter) =>
        Expression.Lambda<Func<T, object?>>(body.Type == typeof(object) ? body : Expression.Convert(body, typeof(object)), parameter);

    private sealed record SortKey<T>(Expression<Func<T, object?>> Key, bool Descending);

    private readonly record struct MemberKey(Type SourceType, Type ResultType, MemberInfo Member);
}
