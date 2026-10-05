using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

public interface IOrganizationUserService
{
    Task<Result<PagedResult<OrganizationUserResponse>>> ListAsync(ListOrganizationUsersRequest request,
        CancellationToken cancellationToken);
    Task<Result<OrganizationUserFilterCountsResponse>> GetFilterCountsAsync(ListOrganizationUsersRequest request,
        CancellationToken cancellationToken);
    Task<Result<OrganizationUserResponse>> GetAsync(Guid memberId, CancellationToken cancellationToken);
    Task<Result<OrganizationInvitationIssuedResponse>> InviteAsync(InviteOrganizationUserRequest request,
        CancellationToken cancellationToken);
    Task<Result<Guid>> ResendInvitationAsync(Guid invitationId, CancellationToken cancellationToken);
    Task<Result> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken);
    /// <summary>Desde la ficha de una empresa: solo las invitaciones que incluyen una membresía activa en ella.</summary>
    Task<Result<Guid>> ResendCompanyInvitationAsync(Guid companyId, Guid invitationId,
        CancellationToken cancellationToken);
    Task<Result> RevokeCompanyInvitationAsync(Guid companyId, Guid invitationId, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> UpdateAsync(UpdateOrganizationUserRequest request, CancellationToken cancellationToken);
    Task<Result<VersionResponse>> ChangeStatusAsync(ChangeOrganizationUserStatusRequest request, CancellationToken cancellationToken);
    Task<Result> RemoveAsync(Guid memberId, uint? version, CancellationToken cancellationToken);
}
