using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

#pragma warning disable EF1001 // EF no tiene un punto público por ejecución de consulta: el compilador es la única puerta común (ADR 0065).
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>
/// Cada consulta de la base corre con su base fijada en <see cref="PersistenceOperation"/>, también la que se recorre después
/// (una lista, sincrónica o asincrónica): cada paso del recorrido vuelve a fijar la suya, así dos bases recorridas a la vez en el
/// mismo flujo no se mezclan. Lo que arranca enseguida (un <c>FirstOrDefaultAsync</c>, un <c>ExecuteUpdateAsync</c>) se lleva la
/// base en su flujo desde el primer paso. Una consulta compilada a mano (<c>EF.CompileQuery</c>) no pasa por acá y queda sin base.
/// </summary>
internal sealed class OperationQueryCompiler(
    IQueryContextFactory queryContextFactory,
    ICompiledQueryCache compiledQueryCache,
    ICompiledQueryCacheKeyGenerator compiledQueryCacheKeyGenerator,
    IDatabase database,
    IDiagnosticsLogger<DbLoggerCategory.Query> logger,
    ICurrentDbContext currentContext,
    IEvaluatableExpressionFilter evaluatableExpressionFilter,
    IModel model)
    : QueryCompiler(queryContextFactory, compiledQueryCache, compiledQueryCacheKeyGenerator, database, logger, currentContext,
        evaluatableExpressionFilter, model)
{
    private readonly ApplicationDbContext? context = currentContext.Context as ApplicationDbContext;

    public override TResult Execute<TResult>(Expression query)
    {
        if (context is null) return base.Execute<TResult>(query);
        using (PersistenceOperation.Enter(context))
        {
            var result = base.Execute<TResult>(query);
            return Bound<TResult>.Wrap is { } wrap && result is not null ? wrap(result, context) : result;
        }
    }

    public override TResult ExecuteAsync<TResult>(Expression query, CancellationToken cancellationToken)
    {
        if (context is null) return base.ExecuteAsync<TResult>(query, cancellationToken);
        using (PersistenceOperation.Enter(context))
        {
            var result = base.ExecuteAsync<TResult>(query, cancellationToken);
            return Bound<TResult>.Wrap is { } wrap && result is not null ? wrap(result, context) : result;
        }
    }

    /// <summary>Solo las listas genéricas se recorren después; lo demás ya corrió o se llevó la base en su flujo.</summary>
    private static class Bound<TResult>
    {
        internal static readonly Func<TResult, ApplicationDbContext, TResult>? Wrap = Create();

        private static Func<TResult, ApplicationDbContext, TResult>? Create()
        {
            var type = typeof(TResult);
            if (!type.IsGenericType) return null;
            var definition = type.GetGenericTypeDefinition();
            var wrapper = definition == typeof(IAsyncEnumerable<>) ? typeof(BoundAsyncEnumerable<>)
                : definition == typeof(IEnumerable<>) ? typeof(BoundEnumerable<>) : null;
            if (wrapper is null) return null;
            var (inner, context) = (Expression.Parameter(type, "inner"), Expression.Parameter(typeof(ApplicationDbContext), "context"));
            var constructor = wrapper.MakeGenericType(type.GetGenericArguments()).GetConstructor([type, typeof(ApplicationDbContext)])!;
            return Expression.Lambda<Func<TResult, ApplicationDbContext, TResult>>(
                Expression.Convert(Expression.New(constructor, inner, context), type), inner, context).Compile();
        }
    }

    private sealed class BoundEnumerable<T>(IEnumerable<T> inner, ApplicationDbContext context) : IEnumerable<T>
    {
        public IEnumerator<T> GetEnumerator()
        {
            using (PersistenceOperation.Enter(context))
                return new Enumerator(inner.GetEnumerator(), context);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class Enumerator(IEnumerator<T> inner, ApplicationDbContext context) : IEnumerator<T>
        {
            public T Current => inner.Current;

            object? IEnumerator.Current => inner.Current;

            public bool MoveNext()
            {
                using (PersistenceOperation.Enter(context))
                    return inner.MoveNext();
            }

            public void Reset() => inner.Reset();

            public void Dispose() => inner.Dispose();
        }
    }

    private sealed class BoundAsyncEnumerable<T>(IAsyncEnumerable<T> inner, ApplicationDbContext context) : IAsyncEnumerable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            using (PersistenceOperation.Enter(context))
                return new Enumerator(inner.GetAsyncEnumerator(cancellationToken), context);
        }

        /// <summary>El paso arranca con la base fijada y se la lleva en su flujo aunque espere; al volver, se suelta.</summary>
        private sealed class Enumerator(IAsyncEnumerator<T> inner, ApplicationDbContext context) : IAsyncEnumerator<T>
        {
            public T Current => inner.Current;

            public ValueTask<bool> MoveNextAsync()
            {
                using (PersistenceOperation.Enter(context))
                    return inner.MoveNextAsync();
            }

            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
#pragma warning restore EF1001
