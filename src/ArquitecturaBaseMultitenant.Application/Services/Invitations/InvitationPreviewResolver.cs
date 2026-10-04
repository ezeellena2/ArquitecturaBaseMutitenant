using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>
/// Prueba el token antes de entrar y mantiene la continuación únicamente después de leer sus datos reales. La entrada
/// a la organización del token la hace <see cref="InvitationService"/>.
/// </summary>
internal sealed class InvitationPreviewResolver(IInvitationTokenProtector tokens, InvitationPreviewBuilder previews,
    IInvitationFlowContext flow, TimeProvider timeProvider)
{
    /// <summary>El token del pedido o, al recargar, el de la continuación. Sin un token válido, borra la continuación.</summary>
    internal ProvenInvitationToken? ReadToken(PreviewInvitationRequest request)
    {
        var previous = flow.ReadContinuation();
        var token = request.Token ?? previous?.Token;
        var proof = token is null ? null : tokens.Unprotect(token);
        if (proof is null)
        {
            flow.ClearContinuation();
            return null;
        }
        return new ProvenInvitationToken(token!, proof, previous);
    }

    /// <summary>Corre adentro de la organización del token.</summary>
    internal async Task<InvitationPreviewResponse> PreviewAsync(ProvenInvitationToken token, CancellationToken ct)
    {
        var preview = await previews.BuildAsync(token.Proof, tokens.Hash(token.Proof.Secret), ct);
        // Una aceptación al límite del vencimiento conserva hasta cinco minutos para recuperar su respuesta.
        // La cookie no extiende la vigencia de la invitación: se relee ExpiresAtUtc antes de aceptar.
        var previous = token.Continuation;
        if (preview.ExpiresAtUtc is { } expiry && expiry.AddMinutes(5) > timeProvider.GetUtcNow().UtcDateTime)
            flow.Remember(new InvitationContinuation(token.Token, previous is not null && previous.Token == token.Token
                ? previous.Nonce : tokens.GenerateSecret(), expiry.AddMinutes(5)));
        else flow.ClearContinuation();
        return preview;
    }
}
