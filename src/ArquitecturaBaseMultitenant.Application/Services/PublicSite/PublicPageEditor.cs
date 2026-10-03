using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.PublicSite;

/// <summary>Guarda contenido, slug y publicación de la página dentro de la UoW de la petición.</summary>
internal sealed class PublicPageEditor(IPublicPageRepository pages, ITenantRepository tenants,
    ITenantSlugAvailabilityReader slugs, ITenantContext tenantContext, IUnitOfWork unitOfWork,
    IAuditLog auditLog)
{
    private const string TenantSlugConstraint = "IX_Tenants_Slug";
    private static readonly string[] AuditedFields = ["slug", "name", "description", "logo", "contact"];

    internal async Task<Result> UpdateAsync(UpdatePublicPageRequest request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.RequiredTenantId;
        var slug = TenantSlug.Create(request.Slug);
        if (slug.IsFailure) return slug.Error;
        var decodedLogo = DecodeLogo(request);
        if (decodedLogo.IsFailure) return decodedLogo.Error;

        try
        {
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var tenant = await tenants.GetByIdAsync(tenantId, ct);
                if (tenant is null || tenant.Kind != TenantKind.Business) return PublicPageErrors.NotFound;
                var slugChanged = !StringComparer.Ordinal.Equals(tenant.Slug, slug.Value.Value);
                if (slugChanged)
                {
                    if (await slugs.IsReservedAsync(slug.Value.Value, ct)) return TenantSlugErrors.Reserved;
                    if (await slugs.IsTakenByOtherAsync(slug.Value.Value, tenantId, ct)) return TenantSlugErrors.Taken;
                }

                var page = await pages.GetCurrentAsync(request.Version!.Value, ct);
                if (page is null && request.Version.Value != 0) return PublicPageErrors.VersionConflict;
                var isNew = page is null;
                page ??= PublicPage.Create();
                var updated = page.UpdateContent(request.Name, request.Description, request.ContactEmail,
                    request.ContactPhone, request.Address, request.Website);
                if (updated.IsFailure) return updated;
                if (request.RemoveLogo || request.LogoBase64 is not null)
                {
                    var logo = page.SetLogo(decodedLogo.Value, request.RemoveLogo ? null : request.LogoContentType);
                    if (logo.IsFailure) return logo;
                }

                if (slugChanged)
                {
                    var slugResult = tenant.SetSlug(slug.Value);
                    if (slugResult.IsFailure) return slugResult;
                }

                if (isNew) pages.Add(page);
                else pages.MarkUpdated(page);
                auditLog.Record(isNew ? AuditAction.Created : AuditAction.Updated, "PublicPage", page.Id,
                    new Dictionary<string, object?> { ["fields"] = AuditedFields });
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == TenantSlugConstraint)
        {
            return TenantSlugErrors.Taken;
        }
    }

    internal Task<Result> SetPublicationAsync(SetPublicPagePublicationRequest request, bool publish,
        CancellationToken cancellationToken) => unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var page = await pages.GetCurrentAsync(request.Version!.Value, ct);
            if (page is null) return PublicPageErrors.NotFound;
            var changed = publish ? page.Publish() : page.Unpublish();
            if (changed.IsFailure) return changed;
            pages.MarkUpdated(page);
            auditLog.Record(AuditAction.Updated, "PublicPage", page.Id,
                new Dictionary<string, object?> { ["isPublished"] = publish });
            return Result.Success();
        }, CommitPolicy.OnSuccess, cancellationToken);

    private static Result<byte[]?> DecodeLogo(UpdatePublicPageRequest request)
    {
        if (request.RemoveLogo || request.LogoBase64 is null) return Result.Success<byte[]?>(null);
        try
        {
            return Result.Success<byte[]?>(Convert.FromBase64String(request.LogoBase64));
        }
        catch (FormatException)
        {
            return PublicPageErrors.InvalidLogo;
        }
    }
}
