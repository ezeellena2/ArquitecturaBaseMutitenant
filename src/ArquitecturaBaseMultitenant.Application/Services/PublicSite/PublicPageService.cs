using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.PublicSite;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.PublicSite;

internal sealed class PublicPageService(IPublicPageReader reader, ITenantReader tenants,
    ITenantContext tenantContext, PublicPageEditor editor, IPhoneNumberParser phoneParser,
    IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider timeProvider,
    ILogger<PublicPageService> logger) : IPublicPageService
{
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

    public Task<Result<VersionResponse>> UpdateAsync(UpdatePublicPageRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<VersionResponse>(logger, timeProvider, "UpdateOrganizationPublicPage", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var slug = TenantSlug.Create(request.Slug);
            if (slug.IsFailure) return slug.Error;
            var logo = DecodeLogo(request);
            if (logo.IsFailure) return logo.Error;
            var contactPhone = ParseContactPhone(request);
            if (contactPhone.IsFailure) return contactPhone.Error;
            try
            {
                return VersionResponse.Of(await unitOfWork.ExecuteInTransactionAsync(
                    ct => editor.UpdateAsync(request, slug.Value, logo.Value, contactPhone.Value, ct),
                    CommitPolicy.OnSuccess, cancellationToken));
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueKeys.TenantSlug)
            {
                return TenantSlugErrors.Taken;
            }
            catch (UniqueConstraintViolationException exception) when (exception.ConstraintName == UniqueKeys.PublicPage)
            {
                // Dos primeros guardados a la vez: los dos vieron la versión 0 y el otro guardó antes (decisión 28).
                throw new ConcurrencyConflictException("The public page was first saved concurrently.", exception);
            }
        });
    }

    public Task<Result<VersionResponse>> PublishAsync(SetPublicPagePublicationRequest request,
        CancellationToken cancellationToken) => SetPublicationAsync(request, publish: true, cancellationToken);

    public Task<Result<VersionResponse>> UnpublishAsync(SetPublicPagePublicationRequest request,
        CancellationToken cancellationToken) => SetPublicationAsync(request, publish: false, cancellationToken);

    private Task<Result<VersionResponse>> SetPublicationAsync(SetPublicPagePublicationRequest request, bool publish,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<VersionResponse>(logger, timeProvider,
            publish ? "PublishOrganizationPublicPage" : "UnpublishOrganizationPublicPage",
            async () =>
            {
                if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
                return VersionResponse.Of(await unitOfWork.ExecuteInTransactionAsync(
                    ct => editor.SetPublicationAsync(request, publish, ct), CommitPolicy.OnSuccess, cancellationToken));
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

    private Result<PhoneNumber?> ParseContactPhone(UpdatePublicPageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ContactPhoneNumber)) return Result.Success<PhoneNumber?>(null);
        var phone = phoneParser.Parse(request.ContactPhoneCountry, request.ContactPhoneNumber, PhoneUsage.Any);
        if (phone.IsFailure) return FieldErrors.On(phone.Error, "contactPhone");
        return phone.Value;
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
