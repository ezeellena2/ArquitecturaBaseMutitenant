using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers;

/// <summary>Cursor del módulo ligado a su consulta; las reglas de propiedad siempre se aplican antes de paginar.</summary>
internal static class AssistantCursorPagination
{
    private static readonly ConstructorInfo RowConstructor = typeof(ValueTuple<DateTime, Guid>)
        .GetConstructor([typeof(DateTime), typeof(Guid)])!;

    internal static async Task<CursorResult<T>> ToCursorResultAsync<T>(IQueryable<T> query, int limit, string? cursor,
        string scope, bool ascending, Expression<Func<T, DateTime>> sortUtc, Expression<Func<T, Guid>> id, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, CursorRequest.MaxLimit);
        var prefix = "1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope))) + ":";
        if (cursor is not null)
        {
            if (!cursor.StartsWith(prefix, StringComparison.Ordinal) || !CursorCodec.TryDecode(cursor[prefix.Length..], out var position))
                throw new InvalidCursorException();
            var parameter = sortUtc.Parameters[0];
            var rowId = new ReplaceParameterVisitor(id.Parameters[0], parameter).Visit(id.Body)!;
            var row = Expression.Convert(Expression.New(RowConstructor, sortUtc.Body, rowId), typeof(ITuple));
            var after = Expression.Convert(Expression.Constant(ValueTuple.Create(position.SortUtc, position.Id)), typeof(ITuple));
            var compare = typeof(NpgsqlDbFunctionsExtensions).GetMethod(ascending
                ? nameof(NpgsqlDbFunctionsExtensions.GreaterThan) : nameof(NpgsqlDbFunctionsExtensions.LessThan),
                [typeof(DbFunctions), typeof(ITuple), typeof(ITuple)])!;
            query = query.Where(Expression.Lambda<Func<T, bool>>(Expression.Call(compare,
                Expression.Property(null, typeof(EF), nameof(EF.Functions)), row, after), parameter));
        }
        var ordered = ascending ? query.OrderBy(sortUtc).ThenBy(id) : query.OrderByDescending(sortUtc).ThenByDescending(id);
        var items = await ordered.Take(limit + 1).ToListAsync(ct);
        var hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);
        var next = hasMore ? prefix + CursorCodec.Encode(sortUtc.Compile()(items[^1]), id.Compile()(items[^1])) : null;
        return new CursorResult<T>(items, next, hasMore);
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : node;
    }
}
