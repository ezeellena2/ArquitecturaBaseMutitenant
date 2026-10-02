using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Crea la organización y su primera empresa bajo una sola operación idempotente.</summary>
internal sealed class BusinessOrganizationCreator(
    ITenantScope tenantScope,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IUserRepository users,
    ILoginMethodRepository loginMethods,
    ITenantAdminReader tenantAdmins,
    IPlatformSettingsReader platformSettings,
    ITenantSlugAvailabilityReader slugs,
    ITenantRepository tenants,
    ICompanyRepository companies,
    ITenantSettingsRepository tenantSettings,
    ILegalRepository legal,
    IRequestInfo requestInfo,
    ITaxIdTypeCatalog taxIdTypes,
    IPersonalSpaceProvisioner defaults,
    TenantProvisioner provisioner,
    TimeProvider timeProvider,
    ILogger<BusinessOrganizationCreator> logger)
{
    public Task<Result<BusinessSignupResponse>> CreateAsync(Guid userId, Guid operationId,
        BusinessSignupRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<BusinessSignupResponse>(logger, timeProvider, "CreateBusinessOrganization",
            async () =>
            {
                if (userId == Guid.Empty || operationId == Guid.Empty)
                    return BusinessSignupErrors.IdentityRequired;
                if (tenantContext.TenantId is not null)
                    return BusinessSignupErrors.ActiveSpace;

                var parsedSlug = TenantSlug.Create(request.Slug);
                if (parsedSlug.IsFailure) return parsedSlug.Error;
                var taxIdResult = await CreateTaxIdAsync(request.TaxId, cancellationToken);
                if (taxIdResult.IsFailure) return taxIdResult.Error;
                var firstCompanyId = StableCompanyId(operationId);

                using var scope = tenantScope.Enter(operationId);
                try
                {
                    return await unitOfWork.ExecuteInTransactionAsync(async ct =>
                    {
                        await loginMethods.LockUserAsync(userId, ct);
                        var account = await users.GetByIdAsync(userId, ct);
                        if (account is null || account.Status == UserStatus.Deleted)
                            return BusinessSignupErrors.IdentityRequired;
                        if (account.Status == UserStatus.Suspended) return AccountErrors.Suspended;
                        if (account.Status == UserStatus.PendingDeletion) return AccountErrors.PendingDeletion;

                        var existing = await tenants.GetByIdAsync(operationId, ct);
                        if (existing is not null)
                            return await ReplayAsync(existing, userId, request, parsedSlug.Value,
                                firstCompanyId, taxIdResult.Value, ct);

                        var settingsRow = await platformSettings.FindAsync(ct)
                            ?? throw new InvalidOperationException("PlatformSettings has not been seeded.");
                        var owned = await tenantAdmins.ListOwnedBusinessTenantIdsAsync(userId, ct);
                        var allowed = PlatformSettings.Create(ConsumerSignupMode.Open,
                            settingsRow.BusinessSignup, settingsRow.MaxOwnedOrganizations)
                            .CanCreateBusinessOrganization(owned.Count);
                        if (allowed.IsFailure) return allowed.Error;

                        if (await slugs.IsReservedAsync(parsedSlug.Value.Value, ct)) return TenantSlugErrors.Reserved;
                        if (await slugs.IsTakenAsync(parsedSlug.Value.Value, ct)) return TenantSlugErrors.Taken;

                        var draft = await defaults.PrepareAsync(account.Culture, account.TimeZoneId, ct);
                        var firstCompany = Company.Create(firstCompanyId, request.FirstCompanyName,
                            taxIdResult.Value, draft.TimeZoneId);
                        if (firstCompany.IsFailure) return firstCompany.Error;

                        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                        var terms = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, ct)
                            ?? throw new InvalidOperationException("The current Terms document has not been seeded.");
                        var privacy = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Privacy, nowUtc, ct)
                            ?? throw new InvalidOperationException("The current Privacy document has not been seeded.");
                        await AcceptIfNeededAsync(userId, terms, nowUtc, ct);
                        await AcceptIfNeededAsync(userId, privacy, nowUtc, ct);

                        var requiresApproval = settingsRow.BusinessSignup == BusinessSignupMode.RequiresApproval;
                        var tenant = Tenant.CreateBusiness(operationId, request.OrganizationName!.Trim(), requiresApproval);
                        var slugResult = tenant.SetSlug(parsedSlug.Value);
                        if (slugResult.IsFailure) return slugResult.Error;
                        if (!requiresApproval)
                        {
                            var activated = tenant.Activate();
                            if (activated.IsFailure) return activated.Error;
                        }

                        companies.Add(firstCompany.Value);
                        var provisioned = await provisioner.StageAsync(tenant, draft.Settings, [userId], userId, ct,
                            firstCompanyId);
                        if (provisioned.IsFailure) return provisioned.Error;
                        await users.RememberBusinessTenantAsync(userId, tenant.Id, ct);
                        return Response(tenant, firstCompany.Value.Id, requiresApproval);
                    }, CommitPolicy.OnSuccess, cancellationToken);
                }
                catch (UniqueConstraintViolationException exception)
                    when (exception.ConstraintName == "IX_Tenants_Slug")
                {
                    return TenantSlugErrors.Taken;
                }
            });

    private async Task<Result<BusinessSignupResponse>> ReplayAsync(Tenant existing, Guid userId,
        BusinessSignupRequest request, TenantSlug slug, Guid firstCompanyId, TaxId? requestedTaxId,
        CancellationToken ct)
    {
        if (existing.Kind != TenantKind.Business || existing.Name != request.OrganizationName!.Trim()
            || existing.Slug != slug.Value)
            return TenantErrors.ProvisioningConflict;

        var company = await companies.GetByIdAsync(firstCompanyId, 0, ct);
        if (company is null || company.Name != request.FirstCompanyName!.Trim()
            || !Equals(company.TaxId, requestedTaxId))
            return TenantErrors.ProvisioningConflict;
        var storedSettings = await tenantSettings.GetCurrentAsync(ct);
        if (storedSettings is null) return TenantErrors.ProvisioningConflict;

        var requiresApproval = existing.Status == TenantStatus.PendingApproval;
        var provisioned = await provisioner.StageAsync(existing, storedSettings, [userId], userId, ct,
            firstCompanyId);
        if (provisioned.IsFailure) return provisioned.Error;
        await users.RememberBusinessTenantAsync(userId, existing.Id, ct);
        return Response(existing, company.Id, requiresApproval);
    }

    private async Task<Result<TaxId?>> CreateTaxIdAsync(BusinessSignupTaxId? request, CancellationToken ct)
    {
        if (request is null) return Result.Success<TaxId?>(null);
        var type = await taxIdTypes.FindAsync(request.Type ?? string.Empty, ct);
        if (type is not { IsEnabled: true } || type.AppliesTo is not ("Company" or "Both"))
            return TaxIdErrors.Invalid;
        var domainType = TaxIdType.Create(type.Code, type.CountryCode, type.Label, type.Mask,
            type.ValidatorKey, type.AppliesTo, type.IsEnabled, type.SortOrder);
        var taxId = TaxId.Create(type.CountryCode, domainType, request.Number);
        return taxId.IsSuccess ? Result.Success<TaxId?>(taxId.Value) : taxId.Error;
    }

    private async Task AcceptIfNeededAsync(Guid userId, LegalDocument document, DateTime nowUtc,
        CancellationToken ct)
    {
        if (await legal.HasAcceptedAsync(userId, document.Id, ct)) return;
        legal.AddAcceptance(LegalAcceptance.Create(userId, document, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
    }

    private static BusinessSignupResponse Response(Tenant tenant, Guid firstCompanyId, bool requiresApproval) =>
        new(tenant.Id, tenant.Name, tenant.Slug!, requiresApproval, firstCompanyId, tenant.Status);

    private static Guid StableCompanyId(Guid tenantId)
    {
        var digest = SHA256.HashData(tenantId.ToByteArray());
        return new Guid(digest.AsSpan(0, 16));
    }
}
