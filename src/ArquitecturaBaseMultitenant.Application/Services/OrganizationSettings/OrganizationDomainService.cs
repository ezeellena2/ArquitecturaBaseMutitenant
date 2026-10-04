using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;

/// <summary>Valida las solicitudes y coordina el ciclo de vida del dominio organizacional.</summary>
internal sealed class OrganizationDomainService(ITenantDomainReader reader, IRequestValidator validator,
    OrganizationDomainRegistrar registrar, OrganizationDomainVerifier verifier,
    OrganizationDomainRemover remover, IUnitOfWork unitOfWork, TimeProvider timeProvider,
    ILogger<OrganizationDomainService> logger) : IOrganizationDomainService
{
    public Task<Result<IReadOnlyList<OrganizationDomainResponse>>> ListAsync(CancellationToken cancellationToken) =>
        OperationLog.RunAsync<IReadOnlyList<OrganizationDomainResponse>>(logger, timeProvider,
            "ListOrganizationDomains", async () =>
            {
                var rows = await reader.ListCurrentAsync(cancellationToken);
                return rows.Select(ToResponse).ToArray();
            });

    public Task<Result<OrganizationDomainResponse>> RegisterAsync(RegisterOrganizationDomainRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<OrganizationDomainResponse>(logger, timeProvider,
            "RegisterOrganizationDomain", async () =>
            {
                if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
                var domain = OrganizationEmailDomain.Create(request.Domain);
                if (domain.IsFailure) return domain.Error;
                return await registrar.RegisterAsync(domain.Value, cancellationToken);
            });
    }

    public Task<Result> VerifyAsync(Guid domainId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "VerifyOrganizationDomain", async () =>
        {
            if (domainId == Guid.Empty) return TenantDomainErrors.NotFound;
            var pending = await reader.FindCurrentByIdAsync(domainId, cancellationToken);
            if (pending is null) return TenantDomainErrors.NotFound;
            var prepared = await verifier.PrepareAsync(pending, cancellationToken);
            if (prepared.IsFailure) return prepared;
            return await unitOfWork.ExecuteInTransactionAsync(ct => verifier.StageAsync(pending, ct),
                CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> RemoveAsync(RemoveOrganizationDomainRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "RemoveOrganizationDomain", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var current = await reader.FindCurrentByIdAsync(request.DomainId, cancellationToken);
            if (current is null) return TenantDomainErrors.NotFound;
            return await unitOfWork.ExecuteInTransactionAsync(ct => remover.StageAsync(current,
                request.Version!.Value, ct), CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    internal static OrganizationDomainResponse ToResponse(OrganizationDomainRow domain) =>
        new(domain.Id, domain.Domain, domain.Status,
            domain.Status == TenantDomainStatus.Pending ? domain.Domain : null,
            domain.Status == TenantDomainStatus.Pending ? TenantDomain.TxtRecordPrefix + domain.VerificationToken : null,
            domain.CreatedAtUtc, domain.VerifiedAtUtc, domain.Version);
}
