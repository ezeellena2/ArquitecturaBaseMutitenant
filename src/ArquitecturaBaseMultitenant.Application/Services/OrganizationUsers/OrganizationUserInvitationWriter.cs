using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>
/// Invita, reenvía y revoca dentro de la transacción del servicio; cada reenvío es una invitación independiente. Desde
/// una empresa, solo las invitaciones que se pueden resolver desde ella (<see cref="CompanyInvitationPolicy"/>): las
/// demás son ajenas (404).
/// </summary>
internal sealed class OrganizationUserInvitationWriter(
    IInvitationRepository invitations,
    InvitationIssuer issuer,
    IExternalLoginLock externalLoginLock,
    ICurrentUser currentUser,
    OrganizationUserAccessRemover accessCleaner,
    OrganizationUserAssignmentWriter assignmentManager,
    TimeProvider timeProvider)
{
    internal async Task<Result<OrganizationInvitationIssuedResponse>> InviteAsync(InviteOrganizationUserRequest request,
        DesiredOrganizationUserAssignments desired, Guid inviterId, CancellationToken ct)
    {
        var issued = await issuer.IssueWithMemberAsync(
            new IssueInvitationRequest(request.Email!, inviterId, InvitationChannel.Email), ct, grant: Grant(desired));
        if (issued.IsFailure) return issued.Error;
        var assigned = await assignmentManager.SetAsync(issued.Value.MemberId, desired, ct,
            memberInUnitOfWork: issued.Value.Member);
        return assigned.IsFailure ? assigned.Error
            : Result<OrganizationInvitationIssuedResponse>.Success(
                new OrganizationInvitationIssuedResponse(issued.Value.InvitationId, issued.Value.ExpiresAtUtc));
    }

    // Lo que el correo muestra como Empresa y Roles, en el mismo orden que la vista previa.
    private static InvitationGrantRow Grant(DesiredOrganizationUserAssignments desired) => new([],
        [.. desired.CompanyRoles.Values.Select(selection => selection.Company.Name).Order(StringComparer.CurrentCulture)],
        [.. desired.OrganizationRoles.Select(role => role.Name).Order(StringComparer.Ordinal)
            .Concat(desired.CompanyRoles.Values.SelectMany(selection => selection.Roles).Select(role => role.Name)
                .Order(StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)]);

    internal async Task<Result<Guid>> ResendAsync(Guid invitationId, CompanyInvitationScope? company,
        CancellationToken ct)
    {
        var locked = await LockInvitationAsync(invitationId, company, ct);
        if (locked.IsFailure) return locked.Error;
        var invitation = locked.Value;
        if (invitation.Status != InvitationStatus.Pending) return InvitationErrors.Invalid;
        var revoked = invitation.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        if (revoked.IsFailure) return revoked.Error;
        if (currentUser.UserId is not { } inviterId || inviterId == Guid.Empty) return AccessErrors.NotMember;
        return await issuer.IssueAsync(new IssueInvitationRequest(invitation.Destination, inviterId,
            invitation.Channel, invitation.MemberId, invitation.Id), ct);
    }

    internal async Task<Result> RevokeAsync(Guid invitationId, CompanyInvitationScope? company, CancellationToken ct)
    {
        var locked = await LockInvitationAsync(invitationId, company, ct);
        if (locked.IsFailure) return locked.Error;
        var revoked = locked.Value.Revoke(timeProvider.GetUtcNow().UtcDateTime);
        if (revoked.IsFailure) return revoked;
        return await accessCleaner.RemoveUnacceptedMemberIfOrphanedAsync(invitationId, locked.Value.MemberId, ct);
    }

    // Toma los locks del destino y de la invitación, y la relee. Desde una empresa, la que no se puede resolver desde
    // ella responde como la que no existe; desde Usuarios, las dos siguen siendo Invalid.
    private async Task<Result<Invitation>> LockInvitationAsync(Guid invitationId, CompanyInvitationScope? company,
        CancellationToken ct)
    {
        var missing = company is null ? InvitationErrors.Invalid : InvitationErrors.NotFound;
        var firstRead = await invitations.GetByIdAsync(invitationId, ct);
        if (firstRead is null) return missing;
        await externalLoginLock.LockEmailAsync(firstRead.Destination, ct);
        await invitations.LockDestinationAsync(firstRead.Destination, ct);
        await invitations.LockAsync(invitationId, ct);
        var invitation = await invitations.GetByIdAsync(invitationId, ct);
        if (invitation is null) return missing;
        if (company is not null && !await CanResolveAsync(company, invitation.MemberId, ct)) return missing;
        return invitation;
    }

    // Lo que da la invitación son los roles y las membresías de su persona.
    private async Task<bool> CanResolveAsync(CompanyInvitationScope company, Guid memberId, CancellationToken ct)
    {
        var granted = await assignmentManager.GetSnapshotAsync(memberId, ct);
        return CompanyInvitationPolicy.CanResolve(company,
            granted.Assignments.Any(assignment => assignment.CompanyId is null),
            [.. granted.CompanyMemberships.Select(membership => (membership.CompanyId, membership.Status))]);
    }
}
