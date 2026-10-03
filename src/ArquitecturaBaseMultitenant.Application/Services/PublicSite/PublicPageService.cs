using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.PublicSite;

internal sealed class PublicPageService(IPublicPageReader reader, IPublicPageRepository pages,
    ITenantReader tenants, ITenantRepository tenantRepository, ITenantSlugAvailabilityReader slugs,
    ITenantContext tenantContext, IRequestValidator validator, IUnitOfWork unitOfWork,
    IAuditLog auditLog, TimeProvider timeProvider, ILogger<PublicPageService> logger) : IPublicPageService
{
    private const string TenantSlugConstraint = "IX_Tenants_Slug";
    private static readonly string[] AuditedFields = ["slug", "name", "description", "logo", "contact"];

    public Task<Result<PublicPageResponse>> GetAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<PublicPageResponse>(logger, timeProvider, "GetOrganizationPublicPage", async () =>
        {
            var tenantId = tenantContext.RequiredTenantId;
            var tenant = await tenants.FindByIdAsync(tenantId, cancellationToken);
            if (tenant is null || tenant.Kind != TenantKind.Business) return PublicPageErrors.NotFound;
            var page = await reader.FindCurrentAsync(cancellationToken);
            return page is null
                ? new PublicPageResponse(tenant.Slug, tenant.Name, null, null, null, null, null,
                    null, false, null, null, 0)
                : ToResponse(tenant.Slug, page);
        });

    public Task<Result> UpdateAsync(UpdatePublicPageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "UpdateOrganizationPublicPage", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var tenantId = tenantContext.RequiredTenantId;
            var slug = TenantSlug.Create(request.Slug);
            if (slug.IsFailure) return slug.Error;
            var decodedLogo = DecodeLogo(request);
            if (decodedLogo.IsFailure) return decodedLogo.Error;

            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var tenant = await tenantRepository.GetByIdAsync(tenantId, ct);
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
                        new Dictionary<string, object?>
                        {
                            ["fields"] = AuditedFields,
                        });
                    return Result.Success();
                }, CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == TenantSlugConstraint)
            {
                return TenantSlugErrors.Taken;
            }
        });
    }

    public Task<Result> PublishAsync(SetPublicPagePublicationRequest request, CancellationToken cancellationToken) =>
        SetPublicationAsync(request, publish: true, cancellationToken);

    public Task<Result> UnpublishAsync(SetPublicPagePublicationRequest request, CancellationToken cancellationToken) =>
        SetPublicationAsync(request, publish: false, cancellationToken);

    private Task<Result> SetPublicationAsync(SetPublicPagePublicationRequest request, bool publish,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, publish ? "PublishOrganizationPublicPage" : "UnpublishOrganizationPublicPage",
            async () =>
            {
                if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
                return await unitOfWork.ExecuteInTransactionAsync(async ct =>
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
            });
    }

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

    private static PublicPageResponse ToResponse(string? slug, PublicPageRow page)
    {
        var logo = page.LogoBytes is null || page.LogoContentType is null
            ? null
            : $"data:{page.LogoContentType};base64,{Convert.ToBase64String(page.LogoBytes)}";
        return new PublicPageResponse(slug, page.Name, page.Description, logo, page.ContactEmail,
            page.ContactPhone, page.Address, page.Website, page.IsPublished,
            page.PublishBlockedAtUtc, page.PublishBlockedReason, page.Version);
    }
}
