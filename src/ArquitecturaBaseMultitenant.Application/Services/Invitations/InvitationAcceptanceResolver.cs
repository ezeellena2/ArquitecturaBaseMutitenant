using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

internal sealed class InvitationAcceptanceResolver(IInvitationTokenProtector tokens, ITenantScope scopes,
    IInvitationFlowContext flow, IInvitationReader invitations, IUserLookup lookup, IRequestValidator validator)
{
    internal async Task<Result<InvitationAcceptanceContext>> PrepareAsync(AcceptInvitationRequest request, CancellationToken ct)
    {
        var continuation = flow.ReadContinuation();
        var token = request.Token ?? continuation?.Token;
        var proof = token is null ? null : tokens.Unprotect(token);
        if (proof is null) return InvitationErrors.Invalid;
        using var scope = scopes.Enter(proof.TenantId);
        var hash = tokens.Hash(proof.Secret);
        var row = await invitations.FindByIdAsync(proof.InvitationId, ct);
        if (row is null || row.TenantId != proof.TenantId || row.TenantKind != TenantKind.Business
            || !InvitationPreviewBuilder.HashEquals(row.TokenHash, hash)) return InvitationErrors.Invalid;
        if (row.Status == InvitationStatus.Accepted) return InvitationErrors.AlreadyUsed;
        if (row.Status == InvitationStatus.Revoked) return InvitationErrors.Invalid;
        var owners = await lookup.FindVerifiedUsersByEmailAsync(row.Destination, ct);
        if (owners.Count > 1) return InvitationErrors.WrongAccount;
        if (await validator.ValidateAsync(request with { RequiresRegistration = owners.Count == 0 }, ct) is { } invalid)
            return invalid;
        return new InvitationAcceptanceContext(proof, token!, hash,
            continuation is not null && continuation.Token == token ? continuation.Nonce : tokens.GenerateSecret(),
            row.Destination, owners.Count == 0 ? null : owners[0], await flow.GetUserIdAsync(ct));
    }

    internal IDisposable Enter(InvitationAcceptanceContext context) => scopes.Enter(context.Proof.TenantId);
}
