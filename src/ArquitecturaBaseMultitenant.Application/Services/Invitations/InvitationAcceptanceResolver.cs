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

/// <summary>
/// Prueba el token y, adentro de su organización, lee la invitación y la cuenta que la puede aceptar, antes de la
/// transacción. La entrada a la organización la hace <see cref="InvitationService"/>.
/// </summary>
internal sealed class InvitationAcceptanceResolver(IInvitationTokenProtector tokens, IInvitationFlowContext flow,
    IInvitationReader invitations, IUserLookup lookup, IRequestValidator validator)
{
    /// <summary>El token del pedido o el de la continuación; null si falta o no es válido.</summary>
    internal ProvenInvitationToken? ReadToken(AcceptInvitationRequest request)
    {
        var continuation = flow.ReadContinuation();
        var token = request.Token ?? continuation?.Token;
        var proof = token is null ? null : tokens.Unprotect(token);
        return proof is null ? null : new ProvenInvitationToken(token!, proof, continuation);
    }

    /// <summary>Corre adentro de la organización del token.</summary>
    internal async Task<Result<InvitationAcceptanceContext>> PrepareAsync(AcceptInvitationRequest request,
        ProvenInvitationToken token, CancellationToken ct)
    {
        var proof = token.Proof;
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
        var continuation = token.Continuation;
        return new InvitationAcceptanceContext(proof, token.Token, hash,
            continuation is not null && continuation.Token == token.Token ? continuation.Nonce : tokens.GenerateSecret(),
            row.Destination, owners.Count == 0 ? null : owners[0], await flow.GetUserIdAsync(ct));
    }
}
