using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Administra empresas del tenant activo y asigna al creador como su primer administrador.</summary>
internal sealed class CompanyService(
    ICompanyReader reader,
    ICompanyRepository companies,
    ICompanyMembershipRepository memberships,
    IMemberRepository members,
    IRoleRepository roles,
    IRoleAssignmentRepository assignments,
    ITaxIdTypeCatalog taxIdTypes,
    ITimeZoneCatalog timeZones,
    ICurrentUser currentUser,
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
            var taxId = await CreateTaxIdAsync(request.TaxIdType, request.TaxIdNumber, cancellationToken);
            if (taxId.IsFailure) return taxId.Error;
            if (!await IsAvailableTimeZoneAsync(request.TimeZoneId!, cancellationToken))
                return CompanyErrors.TimeZoneUnavailable;

            var created = Company.Create(request.Name, taxId.Value, request.TimeZoneId);
            if (created.IsFailure) return created.Error;
            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(ct => CreateCoreAsync(created.Value, ct),
                    CommitPolicy.OnSuccess, cancellationToken);
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
            var taxId = await CreateTaxIdAsync(request.TaxIdType, request.TaxIdNumber, cancellationToken);
            if (taxId.IsFailure) return taxId.Error;
            if (!await IsAvailableTimeZoneAsync(request.TimeZoneId!, cancellationToken))
                return CompanyErrors.TimeZoneUnavailable;

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
                    var updated = company.Update(request.Name, taxId.Value, request.TimeZoneId);
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

    private async Task<Result<Guid>> CreateCoreAsync(Company company, CancellationToken cancellationToken)
    {
        if (company.TaxId is { } taxId && await reader.ExistsTaxIdAsync(taxId.Country, taxId.Type,
                taxId.Number, excludedCompanyId: null, cancellationToken))
            return CompanyErrors.TaxIdAlreadyUsed;
        if (currentUser.UserId is not { } userId || userId == Guid.Empty)
            return AccessErrors.NotMember;

        var member = await members.GetByUserIdAsync(userId, cancellationToken);
        if (member is null) return AccessErrors.NotMember;
        if (member.Status != MemberStatus.Active) return MemberErrors.Inactive;
        var adminRole = await roles.GetByNameAsync(SystemRoles.CompanyAdmin, cancellationToken);
        if (adminRole is null) throw new InvalidOperationException("The CompanyAdmin system role has not been seeded.");
        var membership = CompanyMembership.Create(company.Id, member.Id);
        if (membership.IsFailure) return membership.Error;
        var assignment = RoleAssignment.Assign(member.Id, adminRole, company.Id);
        if (assignment.IsFailure) return assignment.Error;

        companies.Add(company);
        memberships.Add(membership.Value);
        assignments.Add(assignment.Value);
        return company.Id;
    }

    private async Task<Result<TaxId?>> CreateTaxIdAsync(string? typeCode, string? number,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(typeCode) && string.IsNullOrWhiteSpace(number))
            return Result.Success<TaxId?>(null);
        var type = await taxIdTypes.FindAsync(typeCode ?? string.Empty, cancellationToken);
        if (type is not { IsEnabled: true } || type.AppliesTo is not ("Company" or "Both"))
            return TaxIdErrors.Invalid;
        var domainType = TaxIdType.Create(type.Code, type.CountryCode, type.Label, type.Mask,
            type.ValidatorKey, type.AppliesTo, type.IsEnabled, type.SortOrder);
        var result = TaxId.Create(type.CountryCode, domainType, number);
        return result.IsSuccess ? Result.Success<TaxId?>(result.Value) : TaxIdErrors.Invalid;
    }

    private async Task<bool> IsAvailableTimeZoneAsync(string id, CancellationToken cancellationToken) =>
        await timeZones.FindAsync(id, cancellationToken) is { IsEnabled: true };

    private static CompanyResponse ToResponse(CompanyRow company) => new(company.Id, company.Name,
        company.TaxCountry is null || company.TaxType is null || company.TaxNumber is null ? null
            : new CompanyTaxIdResponse(company.TaxCountry, company.TaxType, company.TaxNumber),
        company.TimeZoneId, company.Status, company.MemberCount, company.Version, company.CreatedAtUtc);
}
