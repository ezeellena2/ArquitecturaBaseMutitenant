using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Administra empresas del tenant activo y asigna al creador como su primer administrador.</summary>
internal sealed class CompanyService(
    ICompanyReader reader,
    ICompanyRepository companies,
    CompanyCreator creator,
    CompanyReferenceDataResolver referenceData,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CompanyService> logger) : ICompanyService
{
    private const string TaxIdUniqueConstraint = "IX_Companies_TenantId_TaxCountry_TaxType_TaxNumber";

    public Task<Result<PagedResult<CompanyResponse>>> ListAsync(ListCompaniesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListCompanies", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await reader.ListAsync(request, cancellationToken);
            return Result.Success(new PagedResult<CompanyResponse>([.. page.Items.Select(ToResponse)],
                page.Page, page.PageSize, page.TotalCount));
        });
    }

    public Task<Result<CompanyFilterCountsResponse>> GetFilterCountsAsync(ListCompaniesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "GetCompanyFilterCounts", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return Result.Success(await reader.GetFilterCountsAsync(request, cancellationToken));
        });
    }

    public Task<Result<CompanyResponse>> GetAsync(Guid companyId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<CompanyResponse>(logger, timeProvider, "GetCompany", async () =>
        {
            var company = await reader.FindByIdAsync(companyId, cancellationToken);
            return company is null ? CompanyErrors.NotFound : ToResponse(company);
        });

    public Task<Result<Guid>> CreateAsync(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "CreateCompany", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var taxId = await referenceData.CreateTaxIdAsync(request.TaxIdType, request.TaxIdNumber, cancellationToken);
            if (taxId.IsFailure) return taxId.Error;
            var timeZone = await referenceData.ResolveTimeZoneAsync(request.TimeZoneId, cancellationToken);
            if (timeZone.IsFailure) return timeZone.Error;

            var created = Company.Create(request.Name, taxId.Value, timeZone.Value);
            if (created.IsFailure) return created.Error;
            try
            {
                return await creator.CreateAsync(created.Value, cancellationToken);
            }
            catch (UniqueConstraintViolationException exception)
                when (exception.ConstraintName == TaxIdUniqueConstraint)
            {
                return CompanyErrors.TaxIdAlreadyUsed;
            }
        });
    }

    public Task<Result> UpdateAsync(UpdateCompanyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "UpdateCompany", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var taxId = await referenceData.CreateTaxIdAsync(request.TaxIdType, request.TaxIdNumber, cancellationToken);
            if (taxId.IsFailure) return taxId.Error;
            var timeZone = await referenceData.ResolveTimeZoneAsync(request.TimeZoneId, cancellationToken);
            if (timeZone.IsFailure) return timeZone.Error;

            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(async ct =>
                {
                    var company = await companies.GetByIdAsync(request.CompanyId, request.Version!.Value, ct);
                    if (company is null) return CompanyErrors.NotFound;
                    if (taxId.Value is { } requestedTaxId
                        && await reader.ExistsTaxIdAsync(requestedTaxId.Country, requestedTaxId.Type,
                            requestedTaxId.Number, company.Id, ct))
                        return CompanyErrors.TaxIdAlreadyUsed;
                    var updated = company.Update(request.Name, taxId.Value, timeZone.Value);
                    if (updated.IsFailure) return updated;
                    companies.MarkUpdated(company);
                    return Result.Success();
                }, CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (UniqueConstraintViolationException exception)
                when (exception.ConstraintName == TaxIdUniqueConstraint)
            {
                return CompanyErrors.TaxIdAlreadyUsed;
            }
        });
    }

    public Task<Result> ChangeStatusAsync(ChangeCompanyStatusRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ChangeCompanyStatus", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var company = await companies.GetByIdAsync(request.CompanyId, request.Version!.Value, ct);
                if (company is null) return CompanyErrors.NotFound;
                company.SetStatus(request.Status!.Value);
                companies.MarkUpdated(company);
                return Result.Success();
            }, CommitPolicy.OnSuccess, cancellationToken);
        });
    }

    private static CompanyResponse ToResponse(CompanyRow company) => new(company.Id, company.Name,
        company.TaxCountry is null || company.TaxType is null || company.TaxNumber is null ? null
            : new CompanyTaxIdResponse(company.TaxCountry, company.TaxType, company.TaxNumber),
        company.TimeZoneId, company.Status, company.MemberCount, company.Version, company.CreatedAtUtc);
}
