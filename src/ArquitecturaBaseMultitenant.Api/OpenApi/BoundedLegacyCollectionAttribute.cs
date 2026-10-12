using ArquitecturaBaseMultitenant.Application.Common.Pagination;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>Conserva un array legacy de una sola página; la ruta sucesora publica paginado y búsqueda.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class BoundedLegacyCollectionAttribute(string successor) : Attribute
{
    public string Successor { get; } = successor;
    public int MaxItems => PagedRequest.DefaultPageSize;
}
