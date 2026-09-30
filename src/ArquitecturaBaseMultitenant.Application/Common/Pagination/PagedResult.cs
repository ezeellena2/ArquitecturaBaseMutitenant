namespace ArquitecturaBaseMultitenant.Application.Common.Pagination;

/// <summary>Representa una página y el total de resultados para que el consumidor pueda mostrar navegación anterior y siguiente.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}
