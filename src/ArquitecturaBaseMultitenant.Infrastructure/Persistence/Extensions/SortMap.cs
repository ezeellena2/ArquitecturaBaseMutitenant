using System.Linq.Expressions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>Campos HTTP ordenables y sus expresiones de consulta para un reader.</summary>
public sealed class SortMap<T> : Dictionary<string, Expression<Func<T, object?>>>
{
    public SortMap() : base(StringComparer.OrdinalIgnoreCase)
    {
    }
}
