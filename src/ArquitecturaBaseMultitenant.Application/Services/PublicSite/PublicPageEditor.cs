using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.PublicSite;

/// <summary>Guarda contenido, slug y publicación de la página dentro de la transacción del servicio.</summary>
internal sealed class PublicPageEditor(IPublicPageRepository pages, ITenantRepository tenants,
    ITenantSlugAvailabilityReader slugs, ITenantContext tenantContext, IAuditLog auditLog)
{
    private static readonly string[] AuditedFields = ["slug", "name", "description", "logo", "contact"];

    internal async Task<Result<PublicPage>> UpdateAsync(UpdatePublicPageRequest request, TenantSlug slug,
        byte[]? logo, PhoneNumber? contactPhone, CancellationToken ct)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null || tenant.Kind != TenantKind.Business) return PublicPageErrors.NotFound;
        var slugChanged = !StringComparer.Ordinal.Equals(tenant.Slug, slug.Value);
        if (slugChanged)
        {
            if (await slugs.IsReservedAsync(slug.Value, ct)) return TenantSlugErrors.Reserved;
            if (await slugs.IsTakenByOtherAsync(slug.Value, tenantId, ct)) return TenantSlugErrors.Taken;
        }

        var page = await pages.GetCurrentAsync(request.Version!.Value, ct);
        // Sin página guardada, la única versión vigente es 0: otra es vieja y responde como cualquier edición
        // simultánea (docs/rules/concurrencia.md).
        if (page is null && request.Version.Value != 0) throw new ConcurrencyConflictException();
        var isNew = page is null;
        page ??= PublicPage.Create();
        var updated = page.UpdateContent(request.Name, request.Description, request.ContactEmail,
            contactPhone, request.Address, request.Website);
        if (updated.IsFailure) return updated.Error;
        if (request.RemoveLogo || request.LogoBase64 is not null)
        {
            var logoSet = page.SetLogo(logo, request.RemoveLogo ? null : request.LogoContentType);
            if (logoSet.IsFailure) return logoSet.Error;
        }

        if (slugChanged)
        {
            var slugResult = tenant.SetSlug(slug);
            if (slugResult.IsFailure) return slugResult.Error;
        }

        if (isNew) pages.Add(page);
        else pages.MarkUpdated(page);
        auditLog.Record(isNew ? AuditAction.Created : AuditAction.Updated, "PublicPage", page.Id,
            new Dictionary<string, object?> { ["fields"] = AuditedFields });
        return page;
    }

    internal async Task<Result<PublicPage>> SetPublicationAsync(SetPublicPagePublicationRequest request, bool publish,
        CancellationToken ct)
    {
        var page = await pages.GetCurrentAsync(request.Version!.Value, ct);
        if (page is null) return PublicPageErrors.NotFound;
        var changed = publish ? page.Publish() : page.Unpublish();
        if (changed.IsFailure) return changed.Error;
        pages.MarkUpdated(page);
        auditLog.Record(AuditAction.Updated, "PublicPage", page.Id,
            new Dictionary<string, object?> { ["isPublished"] = publish });
        return page;
    }
}
