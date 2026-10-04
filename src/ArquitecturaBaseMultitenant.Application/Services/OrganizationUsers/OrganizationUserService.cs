using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Administra miembros e invitaciones sin alterar la cuenta global ni otras organizaciones.</summary>
internal sealed class OrganizationUserService(
    IOrganizationUserReader reader,
    IRequestValidator validator,
    ICurrentUser currentUser,
    OrganizationUserAssignmentManager assignments,
    OrganizationUserInvitationManager invitations,
    OrganizationUserEditor editor,
    OrganizationUserStatusEditor statusEditor,
    OrganizationUserRemover remover,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<OrganizationUserService> logger) : IOrganizationUserService
{
    public Task<Result<PagedResult<OrganizationUserResponse>>> ListAsync(ListOrganizationUsersRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ListOrganizationUsers", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var page = await reader.ListAsync(request, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            return Result.Success(new PagedResult<OrganizationUserResponse>([.. page.Items.Select(ToResponse)],
                page.Page, page.PageSize, page.TotalCount));
        });
    }

    public Task<Result<OrganizationUserFilterCountsResponse>> GetFilterCountsAsync(
        ListOrganizationUsersRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "GetOrganizationUserFilterCounts", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return Result.Success(await reader.GetFilterCountsAsync(request,
                timeProvider.GetUtcNow().UtcDateTime, cancellationToken));
        });
    }

    public Task<Result<OrganizationUserResponse>> GetAsync(Guid memberId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync<OrganizationUserResponse>(logger, timeProvider, "GetOrganizationUser", async () =>
        {
            var row = await reader.FindByIdAsync(memberId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
            return row is null ? MemberErrors.NotFound : ToResponse(row);
        });

    public Task<Result<OrganizationInvitationIssuedResponse>> InviteAsync(InviteOrganizationUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "InviteOrganizationUser", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            if (currentUser.UserId is not { } inviterId || inviterId == Guid.Empty) return AccessErrors.NotMember;
            var desired = await assignments.ValidateAsync(request.RoleIds!, request.Companies!, cancellationToken);
            if (desired.IsFailure) return desired.Error;
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => invitations.InviteAsync(request, desired.Value, inviterId, ct), CommitPolicy.OnSuccess,
                cancellationToken);
        });
    }

    public Task<Result<Guid>> ResendInvitationAsync(Guid invitationId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "ResendOrganizationInvitation", async () =>
        {
            if (invitationId == Guid.Empty) return InvitationErrors.Invalid;
            return await unitOfWork.ExecuteInTransactionAsync(ct => invitations.ResendAsync(invitationId, ct),
                CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "RevokeOrganizationInvitation", async () =>
        {
            if (invitationId == Guid.Empty) return InvitationErrors.Invalid;
            return await unitOfWork.ExecuteInTransactionAsync(ct => invitations.RevokeAsync(invitationId, ct),
                CommitPolicy.OnSuccess, cancellationToken);
        });

    public Task<Result> UpdateAsync(UpdateOrganizationUserRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "UpdateOrganizationUser", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            var desired = await assignments.ValidateAsync(request.RoleIds!, request.Companies!, cancellationToken);
            if (desired.IsFailure) return desired.Error;
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => editor.UpdateAsync(request, desired.Value, currentUser.UserId, ct), CommitPolicy.OnSuccess,
                cancellationToken);
        });
    }

    public Task<Result> ChangeStatusAsync(ChangeOrganizationUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OperationLog.RunAsync(logger, timeProvider, "ChangeOrganizationUserStatus", async () =>
        {
            if (await validator.ValidateAsync(request, cancellationToken) is { } invalid) return invalid;
            return await unitOfWork.ExecuteInTransactionAsync(
                ct => statusEditor.ChangeStatusAsync(request, currentUser.UserId, ct), CommitPolicy.OnSuccess,
                cancellationToken);
        });
    }

    public Task<Result> RemoveAsync(Guid memberId, uint? version, CancellationToken cancellationToken) =>
        OperationLog.RunAsync(logger, timeProvider, "RemoveOrganizationUser", async () =>
        {
            if (memberId == Guid.Empty || version is null) return UserErrors.NotFound;
            return await unitOfWork.ExecuteInTransactionAsync(ct => remover.RemoveAsync(memberId, version.Value, ct),
                CommitPolicy.OnSuccess, cancellationToken);
        });

    private OrganizationUserResponse ToResponse(OrganizationUserRow row) => new(row.MemberId, row.UserId,
        row.DisplayName.Length == 0 ? row.Email?.DisplayValue ?? string.Empty : row.DisplayName, row.Email,
        OrganizationUserStatePolicy.Resolve(row.MemberStatus, row.UserStatus, row.InvitationStatus,
            row.InvitationExpiresAtUtc, timeProvider.GetUtcNow().UtcDateTime), row.HasManagedEmail,
        [.. row.Roles.Select(Translate)],
        [.. row.Companies.Select(company => company with { Roles = [.. company.Roles.Select(Translate)] })],
        row.InvitationId, row.InvitationIssuedAtUtc, row.InvitationExpiresAtUtc, row.Version, row.CreatedAtUtc,
        row.DeletionScheduledForUtc);

    private static OrganizationUserRoleRow Translate(OrganizationUserRoleRow role) => role with
    {
        Name = RoleTexts.Name(role.Name),
    };
}
