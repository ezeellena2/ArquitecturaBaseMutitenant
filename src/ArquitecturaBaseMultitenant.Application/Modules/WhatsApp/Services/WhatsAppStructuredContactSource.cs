using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.StructuredData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.StructuredData;
using ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>
/// Los contactos de la bandeja Clientes para las listas de otra área (C12 de la E12), sin teléfono. Hay contactos solo si WhatsApp
/// está prendido para la organización: lo dice <see cref="IFeatureService"/>, que pasa por la regla de la habilitación de cada
/// organización, nunca una lectura propia de sus funcionalidades.
/// </summary>
internal sealed class WhatsAppStructuredContactSource(IFeatureService features, IWhatsAppContactReader contacts) : IStructuredContactSource
{
    public Task<bool> IsAvailableAsync(CancellationToken ct) => features.IsEnabledAsync(WhatsAppFeature.Key, ct);

    public async Task<StructuredContactPage> ListAsync(StructuredContactQuery query, CancellationToken ct) =>
        await IsAvailableAsync(ct) ? await contacts.ListStructuredAsync(query, ct) : new StructuredContactPage([], 0, ExceedsLimit: false);

    public async Task<StructuredContact?> FindAsync(Guid contactId, CancellationToken ct) =>
        await IsAvailableAsync(ct) ? await contacts.FindStructuredAsync(contactId, ct) : null;

    public async Task<PagedResult<StructuredContact>> ListPageAsync(PagedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await IsAvailableAsync(ct) ? await contacts.ListStructuredPageAsync(request, ct)
            : new PagedResult<StructuredContact>([], request.Page, request.PageSize, 0);
    }

    public async Task<PagedResult<StructuredContactTag>> ListTagsAsync(PagedRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await IsAvailableAsync(ct) ? await contacts.ListTagCountsAsync(request, ct)
            : new PagedResult<StructuredContactTag>([], request.Page, request.PageSize, 0);
    }
}
