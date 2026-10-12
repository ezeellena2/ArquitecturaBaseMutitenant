using ArquitecturaBaseMultitenant.Application.Common.Exceptions;
using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
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
    ICurrentUser currentUser,
    IPermissionService permissions,
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
            var canInvite = currentUser.UserId is { } userId
                && await permissions.HasPermissionAsync(userId, Permissions.Users.Invite, cancellationToken);
            var page = await reader.ListAsync(companyId, request with
            {
                OnlyCompanyInvitations = request.State == CompanyMemberState.Expired && !canInvite,
            }, cancellationToken);
            return Result.Success(new PagedResult<CompanyMemberResponse>(
                [.. page.Items.Select(member => ToResponse(member, canInvite))], page.Page, page.PageSize,
                page.TotalCount));
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

    public Task<Result<PagedResult<CompanyCandidateResponse>>> ListCandidatesAsync(Guid companyId,
        ListCompanyCandidatesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListCompanyCandidates", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (await companies.FindByIdAsync(companyId, cancellationToken) is null) return CompanyErrors.NotFound;
            var page = await reader.ListCandidatesAsync(companyId, request, cancellationToken);
            return Result.Success(new PagedResult<CompanyCandidateResponse>([.. page.Items.Select(ToResponse)],
                page.Page, page.PageSize, page.TotalCount));
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

    // La invitación viene solo si quien mira puede resolverla desde la ficha (decisión 101): con users.invite, la que
    // incluye a la empresa; sin él, la que es únicamente de ella. Así el front muestra el ⋮ solo cuando sirve.
    private static CompanyMemberResponse ToResponse(CompanyMemberRow member, bool canInvite)
    {
        var resolvable = canInvite || member.InvitationOnlyForCompany;
        return new CompanyMemberResponse(member.MemberId, member.UserId, member.DisplayName, member.Email,
            member.State, member.MembershipStatus,
            [.. member.Roles.Select(role => new CompanyMemberRoleResponse(role.Id, RoleTexts.Name(role.Name),
                role.IsSystemRole, role.Scope, role.CompanyId))], member.Version, member.CreatedAtUtc,
            resolvable ? member.InvitationId : null, resolvable ? member.InvitationExpiresAtUtc : null);
    }

    // Quien suma a alguien a su empresa no ve Usuarios: el correo va tapado y, sin nombre, el nombre es ese correo.
    private static CompanyCandidateResponse ToResponse(CompanyCandidateRow candidate)
    {
        var maskedEmail = candidate.Email is { } email ? PersonalDataMask.Email(email.Value) : null;
        return new CompanyCandidateResponse(candidate.MemberId,
            candidate.DisplayName.Length == 0 ? maskedEmail ?? string.Empty : candidate.DisplayName, maskedEmail);
    }
}
