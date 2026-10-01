using System.Security.Cryptography;
using System.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>Lee exclusivamente dentro del alcance probado y no consume ni modifica la invitación.</summary>
internal sealed class InvitationPreviewBuilder(IInvitationReader invitations, IUserLookup lookup,
    ILoginMethodReader methods, LoginMethodAvailability availability, IInvitationFlowContext flow,
    TimeProvider timeProvider)
{
    internal async Task<InvitationPreviewResponse> BuildAsync(InvitationTokenData proof, string tokenHash, CancellationToken ct)
    {
        var row = await invitations.FindByIdAsync(proof.InvitationId, ct);
        if (row is null || row.Id != proof.InvitationId || row.TenantId != proof.TenantId
            || row.TenantKind != TenantKind.Business || !HashEquals(row.TokenHash, tokenHash)
            || row.Status == InvitationStatus.Revoked || row.MemberStatus == MemberStatus.Removed)
            return new(InvitationPreviewState.Invalid);
        var userId = await flow.GetUserIdAsync(ct);
        if (row.Status == InvitationStatus.Accepted)
            return row.AcceptedByUserId is { } accepted && userId == accepted && row.MemberStatus == MemberStatus.Active
                && row.TenantStatus == TenantStatus.Active ? Present(row, InvitationPreviewState.Accepted)
                    : new(InvitationPreviewState.Invalid);
        if (timeProvider.GetUtcNow().UtcDateTime >= row.ExpiresAtUtc) return Present(row, InvitationPreviewState.Expired);
        if (row.TenantStatus == TenantStatus.Suspended) return Present(row, InvitationPreviewState.OrganizationSuspended);
        if (row.TenantStatus != TenantStatus.Active || row.MemberStatus != MemberStatus.Invited)
            return new(InvitationPreviewState.Invalid);
        var owners = await lookup.FindVerifiedUsersByEmailAsync(row.Destination, ct);
        if (owners.Count > 1 || row.MemberUserId is { } bound && (owners.Count != 1 || owners[0] != bound))
            return new(InvitationPreviewState.Invalid);
        var owner = owners.Count == 0 ? (Guid?)null : owners[0];
        var state = userId is not null && userId != owner ? InvitationPreviewState.WrongSession
            : owner is null ? InvitationPreviewState.NoAccount
            : userId is null ? InvitationPreviewState.SignInRequired : InvitationPreviewState.Ready;
        var emailCode = false;
        var google = false;
        if (owner is { } existing)
            foreach (var method in await methods.ListByUserIdAsync(existing, ct))
            {
                if (!await availability.IsAvailableAsync(existing, method, ct)) continue;
                emailCode |= method.Type == LoginMethodType.Email && method.Value == row.Destination.Value;
                google |= method.Type == LoginMethodType.Google && method.ContactEmail == row.Destination;
            }
        string? currentEmail = null;
        if (state == InvitationPreviewState.WrongSession && userId is { } current)
        {
            var ownMethods = await methods.ListByUserIdAsync(current, ct);
            var primary = ownMethods.FirstOrDefault(method => method.IsPrimary && method.VerifiedAtUtc is not null);
            currentEmail = primary?.Type == LoginMethodType.Email ? primary.Value : primary?.ContactEmail?.Value;
        }
        return Present(row, state) with { CanUseEmailCode = emailCode, CanUseGoogle = google, CurrentAccountEmail = currentEmail };
    }

    private static InvitationPreviewResponse Present(InvitationRow row, InvitationPreviewState state) =>
        new(state, row.TenantId, row.OrganizationName, row.Destination,
            row.InviterDeleted ? ErrorTexts.Get("Legal.AccountDeletion.DeletedDisplayName") : row.InviterName ?? row.OrganizationName,
            row.IssuedAtUtc, row.ExpiresAtUtc);

    internal static bool HashEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
