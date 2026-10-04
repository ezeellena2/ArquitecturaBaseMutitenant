using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Alta de empresa: verifica el correo sin crear un espacio Personal y crea la organización con su primera empresa.
/// Cada método abre su única transacción.
/// </summary>
internal sealed class BusinessSignupService(SignupCodeRequester codes, SignupVerificationCore verification,
    IPersonalSpaceProvisioner defaults, ISignInService signIn, ITaxIdTypeCatalog taxIdTypes,
    BusinessOrganizationCreator organizations, ITenantContext tenantContext, ITenantScope tenantScope,
    IRequestValidator validator, IUnitOfWork unitOfWork, TimeProvider timeProvider,
    ILogger<BusinessSignupService> logger) : IBusinessSignupService
{
    public Task<Result<RequestLoginCodeResponse>> RequestEmailCodeAsync(SignupRequest request,
        CancellationToken cancellationToken) =>
        OperationLog.RunAsync<RequestLoginCodeResponse>(logger, timeProvider, "RequestBusinessSignupCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => codes.RequestBusinessSignupAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> VerifyEmailCodeAsync(VerifySignupRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "VerifyBusinessSignupCode", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var draft = await defaults.PrepareAsync(request.Culture, request.TimeZoneId, cancellationToken);
            var verified = await unitOfWork.ExecuteInTransactionAsync(
                ct => verification.VerifyBusinessAsync(request, draft, ct), CommitPolicy.OnAnyResult,
                cancellationToken);
            if (verified.IsFailure) return verified.Error;

            await signIn.SignInAsync(verified.Value, cancellationToken);
            return Result.Success();
        });

    public Task<Result<BusinessSignupResponse>> CreateOrganizationAsync(Guid userId, Guid operationId,
        BusinessSignupRequest request, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<BusinessSignupResponse>(logger, timeProvider, "CreateBusinessSignupOrganization",
            async () =>
            {
                ArgumentNullException.ThrowIfNull(request);
                if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
                if (userId == Guid.Empty || operationId == Guid.Empty)
                    return BusinessSignupErrors.IdentityRequired;
                if (tenantContext.TenantId is not null) return BusinessSignupErrors.ActiveSpace;
                var slug = TenantSlug.Create(request.Slug);
                if (slug.IsFailure) return slug.Error;
                var taxId = await CreateTaxIdAsync(request.TaxId, cancellationToken);
                if (taxId.IsFailure) return taxId.Error;

                // La organización nueva usa el operationId como identificador: se entra a su alcance antes de abrir
                // la transacción.
                using var scope = tenantScope.Enter(operationId);
                try
                {
                    return await unitOfWork.ExecuteInTransactionAsync(
                        ct => organizations.CreateAsync(userId, operationId, request, slug.Value, taxId.Value, ct),
                        CommitPolicy.OnSuccess, cancellationToken);
                }
                catch (UniqueConstraintViolationException exception)
                    when (exception.ConstraintName == "IX_Tenants_Slug")
                {
                    return TenantSlugErrors.Taken;
                }
            });

    private async Task<Result<TaxId?>> CreateTaxIdAsync(BusinessSignupTaxId? request,
        CancellationToken cancellationToken)
    {
        if (request is null) return Result.Success<TaxId?>(null);
        var type = await taxIdTypes.FindAsync(request.Type ?? string.Empty, cancellationToken);
        if (type is not { IsEnabled: true } || type.AppliesTo is not ("Company" or "Both"))
            return TaxIdErrors.Invalid;
        var domainType = TaxIdType.Create(type.Code, type.CountryCode, type.Label, type.Mask,
            type.ValidatorKey, type.AppliesTo, type.IsEnabled, type.SortOrder);
        var taxId = TaxId.Create(type.CountryCode, domainType, request.Number);
        return taxId.IsSuccess ? Result.Success<TaxId?>(taxId.Value) : taxId.Error;
    }
}
