using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Companies;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Companies;

/// <summary>Administra los miembros y roles de una empresa sin alterar sus membresías en otras empresas.</summary>
internal sealed class CompanyMembershipService(
    ICompanyMembershipReader reader,
    ICompanyReader companies,
    CompanyMembershipWriter writer,
    IRequestValidator validator,
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
            return await writer.AddAsync(request, cancellationToken);
        });
    }

    public Task<Result> UpdateRolesAsync(UpdateCompanyMemberRolesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "UpdateCompanyMemberRoles", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await writer.UpdateRolesAsync(request, cancellationToken);
        });
    }

    public Task<Result> ChangeStatusAsync(ChangeCompanyMemberStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ChangeCompanyMemberStatus", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await writer.ChangeStatusAsync(request, cancellationToken);
        });
    }

    private static CompanyMemberResponse ToResponse(CompanyMemberRow member) => new(member.MemberId, member.UserId,
        member.DisplayName, member.Email, GetState(member), member.MembershipStatus,
        [.. member.Roles.Select(role => new CompanyMemberRoleResponse(role.Id, RoleTexts.Name(role.Name),
            role.IsSystemRole, role.Scope, role.CompanyId))], member.Version, member.CreatedAtUtc);

    private static CompanyMemberState GetState(CompanyMemberRow member)
    {
        if (member.MemberStatus == MemberStatus.Invited) return CompanyMemberState.Invited;
        if (member.MembershipStatus == CompanyMembershipStatus.Inactive || member.MemberStatus != MemberStatus.Active
            || member.UserStatus is UserStatus.Suspended or UserStatus.Deleted)
            return CompanyMemberState.Inactive;
        return member.UserStatus == UserStatus.PendingDeletion
            ? CompanyMemberState.PendingDeletion : CompanyMemberState.Active;
    }
}
