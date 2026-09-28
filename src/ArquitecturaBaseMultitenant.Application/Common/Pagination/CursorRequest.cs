namespace ArquitecturaBaseMultitenant.Application.Common.Pagination;

/// <summary>Pedido para un listado que solo crece; el cursor es opaco para el cliente.</summary>
public abstract record CursorRequest
{
    public const int DefaultLimit = 10;

    public const int MaxLimit = 100;

    public string? After { get; init; }

    public int Limit { get; init; } = DefaultLimit;
}
