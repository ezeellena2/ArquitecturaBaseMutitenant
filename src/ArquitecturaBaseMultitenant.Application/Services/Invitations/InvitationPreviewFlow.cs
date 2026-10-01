using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>Prueba el token antes de entrar y mantiene la continuación únicamente después de leer sus datos reales.</summary>
internal sealed class InvitationPreviewFlow(IInvitationTokenProtector tokens, ITenantScope scopes,
    InvitationPreviewBuilder previews, IInvitationFlowContext flow, TimeProvider timeProvider)
{
    internal async Task<InvitationPreviewResponse> PreviewAsync(PreviewInvitationRequest request, CancellationToken ct)
    {
        var previous = flow.ReadContinuation();
        var token = request.Token ?? previous?.Token;
        var proof = token is null ? null : tokens.Unprotect(token);
        if (proof is null)
        {
            flow.ClearContinuation();
            return new(InvitationPreviewState.Invalid);
        }
        using var scope = scopes.Enter(proof.TenantId);
        var preview = await previews.BuildAsync(proof, tokens.Hash(proof.Secret), ct);
        // Una aceptación al límite del vencimiento conserva hasta cinco minutos para recuperar su respuesta.
        // La cookie no extiende la vigencia de la invitación: se relee ExpiresAtUtc antes de aceptar.
        if (preview.ExpiresAtUtc is { } expiry && expiry.AddMinutes(5) > timeProvider.GetUtcNow().UtcDateTime)
            flow.Remember(new InvitationContinuation(token!, previous is not null && previous.Token == token
                ? previous.Nonce : tokens.GenerateSecret(), expiry.AddMinutes(5)));
        else flow.ClearContinuation();
        return preview;
    }
}
