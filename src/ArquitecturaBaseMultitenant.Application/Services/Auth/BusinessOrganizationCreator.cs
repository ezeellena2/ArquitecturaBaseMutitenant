using System.Security.Cryptography;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Crea la organización y su primera empresa bajo una sola operación idempotente.</summary>
internal sealed class BusinessOrganizationCreator(
    BusinessSignupTransactionRunner transaction,
    BusinessSignupPolicyReader policy,
    BusinessSignupOrganizationProvisioner provisioner,
    BusinessSignupLegalAcceptor legalAcceptor,
    BusinessSignupRequestResolver requestResolver,
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
                var noActiveTenant = transaction.EnsureNoActiveTenant();
                if (noActiveTenant.IsFailure) return noActiveTenant.Error;

                var parsedSlug = TenantSlug.Create(request.Slug);
                if (parsedSlug.IsFailure) return parsedSlug.Error;
                var taxId = await requestResolver.CreateTaxIdAsync(request.TaxId, cancellationToken);
                if (taxId.IsFailure) return taxId.Error;
                var firstCompanyId = StableCompanyId(operationId);

                return await transaction.ExecuteAsync(userId, operationId, async (account, ct) =>
                {
                    var existing = await provisioner.FindTenantAsync(operationId, ct);
                    if (existing is not null)
                        return await provisioner.ReplayAsync(existing, userId, request, parsedSlug.Value,
                            firstCompanyId, taxId.Value, account.PrimaryEmail, ct);

                    var eligibility = await policy.EvaluateAsync(userId, parsedSlug.Value, ct);
                    if (eligibility.IsFailure) return eligibility.Error;
                    var draft = await requestResolver.PrepareTenantSpaceAsync(account.Culture,
                        account.TimeZoneId, ct);
                    var firstCompany = Company.Create(firstCompanyId, request.FirstCompanyName!, taxId.Value,
                        draft.TimeZoneId);
                    if (firstCompany.IsFailure) return firstCompany.Error;

                    var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
                    await legalAcceptor.AcceptCurrentAsync(userId, nowUtc, ct);
                    var requiresApproval = eligibility.Value;
                    var tenant = Tenant.CreateBusiness(operationId, request.OrganizationName!.Trim(), requiresApproval);
                    var registration = tenant.RecordRegistration(userId, TenantRegistrationSource.SelfService, nowUtc);
                    if (registration.IsFailure) return registration.Error;
                    var slugResult = tenant.SetSlug(parsedSlug.Value);
                    if (slugResult.IsFailure) return slugResult.Error;
                    if (!requiresApproval)
                    {
                        var activated = tenant.Activate();
                        if (activated.IsFailure) return activated.Error;
                    }

                    var created = await provisioner.CreateAsync(tenant, firstCompany.Value, draft.Settings, userId, ct);
                    if (created.IsFailure) return created.Error;
                    return Response(tenant, firstCompany.Value.Id, requiresApproval, account.PrimaryEmail);
                }, cancellationToken);
            });

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
