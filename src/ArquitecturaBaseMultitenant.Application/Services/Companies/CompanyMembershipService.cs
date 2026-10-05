using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Administra los miembros y roles de una empresa sin alterar sus membresías en otras empresas.</summary>
internal sealed class CompanyMembershipService(
    ICompanyMembershipReader reader,
    ICompanyReader companies,
    CompanyMembershipWriter writer,
    IRequestValidator validator,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<CompanyMembershipService> logger) : ICompanyMembershipService
{
    public Task<Result<PagedResult<CompanyMemberResponse>>> ListAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListCompanyMembers", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var company = await companies.FindByIdAsync(companyId, cancellationToken);
            if (company is null) return CompanyErrors.NotFound;
            var page = await reader.ListAsync(companyId, request, cancellationToken);
            return Result.Success(new PagedResult<CompanyMemberResponse>([.. page.Items.Select(ToResponse)],
                page.Page, page.PageSize, page.TotalCount));
        });
    }

    public Task<Result<CompanyMemberFilterCountsResponse>> GetFilterCountsAsync(Guid companyId,
        ListCompanyMembersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "GetCompanyMemberFilterCounts", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await companies.FindByIdAsync(companyId, cancellationToken) is null) return CompanyErrors.NotFound;
            return Result.Success(await reader.GetFilterCountsAsync(companyId, request, cancellationToken));
        });
    }

    public Task<Result> AddAsync(AddCompanyMemberRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "AddCompanyMember", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            try
            {
                return await unitOfWork.ExecuteInTransactionAsync(ct => writer.AddAsync(request, ct),
                    CommitPolicy.OnSuccess, cancellationToken);
            }
            catch (ForeignKeyViolationException exception)
                when (exception.ConstraintName == ForeignKeys.RoleAssignmentRole)
            {
                // Otro pedido borró el rol después de controlarlo y antes del commit.
                return RoleErrors.NotFound;
            }
        });
    }

    public Task<Result<VersionResponse>> UpdateRolesAsync(UpdateCompanyMemberRolesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<VersionResponse>(logger, timeProvider, "UpdateCompanyMemberRoles", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            try
            {
                return VersionResponse.Of(await unitOfWork.ExecuteInTransactionAsync(
                    ct => writer.UpdateRolesAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken));
            }
            catch (ForeignKeyViolationException exception)
                when (exception.ConstraintName == ForeignKeys.RoleAssignmentRole)
            {
                return RoleErrors.NotFound;
            }
        });
    }

    public Task<Result<VersionResponse>> ChangeStatusAsync(ChangeCompanyMemberStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync<VersionResponse>(logger, timeProvider, "ChangeCompanyMemberStatus", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return VersionResponse.Of(await unitOfWork.ExecuteInTransactionAsync(
                ct => writer.ChangeStatusAsync(request, ct), CommitPolicy.OnSuccess, cancellationToken));
        });
    }

    private static CompanyMemberResponse ToResponse(CompanyMemberRow member) => new(member.MemberId, member.UserId,
        member.DisplayName, member.Email, member.State, member.MembershipStatus,
        [.. member.Roles.Select(role => new CompanyMemberRoleResponse(role.Id, RoleTexts.Name(role.Name),
            role.IsSystemRole, role.Scope, role.CompanyId))], member.Version, member.CreatedAtUtc);
}
