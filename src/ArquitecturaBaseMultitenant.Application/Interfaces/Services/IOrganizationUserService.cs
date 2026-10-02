using ArquitecturaBaseMultitenant.Application.Common.Pagination;
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
    Task<Result<Guid>> InviteAsync(InviteOrganizationUserRequest request, CancellationToken cancellationToken);
    Task<Result<Guid>> ResendInvitationAsync(Guid invitationId, CancellationToken cancellationToken);
    Task<Result> RevokeInvitationAsync(Guid invitationId, CancellationToken cancellationToken);
    Task<Result> UpdateAsync(UpdateOrganizationUserRequest request, CancellationToken cancellationToken);
    Task<Result> ChangeStatusAsync(ChangeOrganizationUserStatusRequest request, CancellationToken cancellationToken);
    Task<Result> RemoveAsync(Guid memberId, uint? version, CancellationToken cancellationToken);
}
