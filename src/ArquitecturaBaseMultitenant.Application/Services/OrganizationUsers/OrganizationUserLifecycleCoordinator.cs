using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Expone las operaciones de edición, estado y baja del ciclo de vida de un miembro.</summary>
internal sealed class OrganizationUserLifecycleCoordinator(
    OrganizationUserEditor editor,
    OrganizationUserStatusEditor statusEditor,
    OrganizationUserRemover remover)
{
    internal Task<Result> UpdateAsync(UpdateOrganizationUserRequest request,
        DesiredOrganizationUserAssignments desired, Guid? currentUserId, CancellationToken cancellationToken) =>
        editor.UpdateAsync(request, desired, currentUserId, cancellationToken);

    internal Task<Result> ChangeStatusAsync(ChangeOrganizationUserStatusRequest request, Guid? currentUserId,
        CancellationToken cancellationToken) =>
        statusEditor.ChangeStatusAsync(request, currentUserId, cancellationToken);

    internal Task<Result> RemoveAsync(Guid memberId, uint version, CancellationToken cancellationToken) =>
        remover.RemoveAsync(memberId, version, cancellationToken);
}
