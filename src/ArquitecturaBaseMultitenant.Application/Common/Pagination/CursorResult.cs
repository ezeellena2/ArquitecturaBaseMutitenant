namespace ArquitecturaBaseMultitenant.Application.Common.Pagination;

/// <summary>Resultado por cursor, sin conteo total sobre tablas que solo crecen.</summary>
public sealed record CursorResult<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);
