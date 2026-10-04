using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Crea la organización con su primera empresa, o reproduce un alta ya hecha, dentro de la transacción que abre
/// <see cref="BusinessSignupService"/>.
/// </summary>
internal sealed class BusinessOrganizationCreator(
    BusinessSignupPolicy policy,
    IUserRepository users,
    ICompanyRepository companies,
    ITenantSettingsRepository tenantSettings,
    TenantProvisioner provisioner,
    IPersonalSpaceProvisioner defaults,
    ILegalRepository legal,
    IRequestInfo requestInfo,
    TimeProvider timeProvider)
{
    internal async Task<Result<BusinessSignupResponse>> CreateAsync(Guid userId, Guid tenantId,
        BusinessSignupRequest request, TenantSlug slug, TaxId? taxId, CancellationToken cancellationToken)
    {
        var decision = await policy.DecideAsync(userId, tenantId, slug, cancellationToken);
        if (decision.IsFailure) return decision.Error;
        var (account, existing, requiresApproval) = decision.Value;
        var firstCompanyId = StableCompanyId(tenantId);
        if (existing is not null)
            return await ReplayAsync(existing, userId, request, slug, firstCompanyId, taxId, requiresApproval,
                account.PrimaryEmail, cancellationToken);

        var draft = await defaults.PrepareAsync(account.Culture, account.TimeZoneId, cancellationToken);
        var firstCompany = Company.Create(firstCompanyId, request.FirstCompanyName!, taxId, draft.TimeZoneId);
        if (firstCompany.IsFailure) return firstCompany.Error;

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        await AcceptCurrentLegalAsync(userId, nowUtc, cancellationToken);
        var tenant = Tenant.CreateBusiness(tenantId, request.OrganizationName!.Trim(), requiresApproval);
        var registration = tenant.RecordRegistration(userId, TenantRegistrationSource.SelfService, nowUtc);
        if (registration.IsFailure) return registration.Error;
        var slugResult = tenant.SetSlug(slug);
        if (slugResult.IsFailure) return slugResult.Error;
        if (!requiresApproval)
        {
            var activated = tenant.Activate();
            if (activated.IsFailure) return activated.Error;
        }

        companies.Add(firstCompany.Value);
        var provisioned = await provisioner.StageAsync(tenant, draft.Settings, [userId], userId,
            cancellationToken, firstCompany.Value.Id);
        if (provisioned.IsFailure) return provisioned.Error;
        await users.RememberBusinessTenantAsync(userId, tenant.Id, cancellationToken);
        return Response(tenant, firstCompany.Value.Id, requiresApproval, account.PrimaryEmail);
    }

    private async Task<Result<BusinessSignupResponse>> ReplayAsync(Tenant existing, Guid userId,
        BusinessSignupRequest request, TenantSlug slug, Guid firstCompanyId, TaxId? requestedTaxId,
        bool requiresApproval, Email? notificationEmail, CancellationToken cancellationToken)
    {
        if (existing.Kind != TenantKind.Business || existing.Name != request.OrganizationName!.Trim()
            || existing.Slug != slug.Value)
            return TenantErrors.ProvisioningConflict;

        var company = await companies.GetByIdAsync(firstCompanyId, 0, cancellationToken);
        if (company is null || company.Name != request.FirstCompanyName!.Trim()
            || !Equals(company.TaxId, requestedTaxId))
            return TenantErrors.ProvisioningConflict;
        var storedSettings = await tenantSettings.GetCurrentAsync(cancellationToken);
        if (storedSettings is null) return TenantErrors.ProvisioningConflict;

        var provisioned = await provisioner.StageAsync(existing, storedSettings, [userId], userId,
            cancellationToken, firstCompanyId);
        if (provisioned.IsFailure) return provisioned.Error;
        await users.RememberBusinessTenantAsync(userId, existing.Id, cancellationToken);
        return Response(existing, company.Id, requiresApproval, notificationEmail);
    }

    // Registra la aceptación vigente de términos y privacidad, si la cuenta todavía no la tiene.
    private async Task AcceptCurrentLegalAsync(Guid userId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var terms = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Terms document has not been seeded.");
        var privacy = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Privacy, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Privacy document has not been seeded.");
        await AcceptIfNeededAsync(userId, terms, nowUtc, cancellationToken);
        await AcceptIfNeededAsync(userId, privacy, nowUtc, cancellationToken);
    }

    private async Task AcceptIfNeededAsync(Guid userId, LegalDocument document, DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (await legal.HasAcceptedAsync(userId, document.Id, cancellationToken)) return;
        legal.AddAcceptance(LegalAcceptance.Create(userId, document, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
    }

    private static BusinessSignupResponse Response(Tenant tenant, Guid firstCompanyId, bool requiresApproval,
        Email? notificationEmail) =>
        new(tenant.Id, tenant.Name, tenant.Slug!, requiresApproval, firstCompanyId, tenant.Status,
            notificationEmail);

    private static Guid StableCompanyId(Guid tenantId)
    {
        var digest = SHA256.HashData(tenantId.ToByteArray());
        return new Guid(digest.AsSpan(0, 16));
    }
}
