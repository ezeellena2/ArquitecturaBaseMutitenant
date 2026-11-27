using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.StructuredData;
using ArquitecturaBaseMultitenant.Application.Models.StructuredData;

namespace ArquitecturaBaseMultitenant.Infrastructure.StructuredData;

/// <summary>
/// Sin un canal de mensajería instalado no hay contactos (C12 de la E12): se registra con <c>TryAdd</c>, y el módulo que da los
/// contactos la reemplaza con la suya.
/// </summary>
internal sealed class DisabledStructuredContactSource : IStructuredContactSource
{
    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(false);

    public Task<StructuredContactPage> ListAsync(StructuredContactQuery query, CancellationToken ct) =>
        Task.FromResult(new StructuredContactPage([], 0, ExceedsLimit: false));

    public Task<StructuredContact?> FindAsync(Guid contactId, CancellationToken ct) => Task.FromResult<StructuredContact?>(null);

    public Task<PagedResult<StructuredContact>> ListPageAsync(PagedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new PagedResult<StructuredContact>([], request.Page, request.PageSize, 0));
    }

    public Task<PagedResult<StructuredContactTag>> ListTagsAsync(PagedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(new PagedResult<StructuredContactTag>([], request.Page, request.PageSize, 0));
    }
}
