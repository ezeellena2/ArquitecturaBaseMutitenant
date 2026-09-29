namespace ArquitecturaBaseMultitenant.Application.Common.Pagination;

/// <summary>
/// Base de las consultas paginadas: <c>?page=2&amp;pageSize=10&amp;sort=-createdAtUtc&amp;search=juan</c>.
/// Cada consulta declara su lista blanca de campos ordenables y la valida con PagedRequestValidator.
/// </summary>
public abstract record PagedRequest
{
    public const int DefaultPage = 1;
    public static IReadOnlyList<int> AllowedPageSizes { get; } = Array.AsReadOnly(new[] { 10, 20, 50, 100 });
    public static int DefaultPageSize => AllowedPageSizes[0];
    public static int MaxPageSize => AllowedPageSizes[^1];

    // Con el máximo permitido, el OFFSET (Page - 1) * PageSize queda muy por debajo de int.MaxValue.
    public const int MaxPage = 1_000_000;

    public const int MaxSearchLength = 100;

    public int Page { get; init; } = DefaultPage;

    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>Campo por el que se ordena; con "-" adelante, descendente.</summary>
    public string? Sort { get; init; }

    public string? Search { get; init; }
}
