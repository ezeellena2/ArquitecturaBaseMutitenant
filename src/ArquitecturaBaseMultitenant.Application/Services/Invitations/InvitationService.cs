using ArquitecturaBaseMultitenant.Application.Common.Logging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

internal sealed class InvitationService(IInvitationTokenProtector tokens, ITenantScope scopes,
    InvitationPreviewBuilder previews, IInvitationFlowContext flow, TimeProvider timeProvider,
    ILogger<InvitationService> logger) : IInvitationService
{
    public Task<Result<InvitationPreviewResponse>> PreviewAsync(PreviewInvitationRequest request, CancellationToken ct) =>
        OperationLog.RunAsync<InvitationPreviewResponse>(logger, timeProvider, "PreviewInvitation", async () =>
        {
            ArgumentNullException.ThrowIfNull(request);
            var previous = flow.ReadContinuation();
            var token = request.Token ?? previous?.Token;
            var proof = token is null ? null : tokens.Unprotect(token);
            if (proof is null)
            {
                flow.ClearContinuation();
                return new InvitationPreviewResponse(InvitationPreviewState.Invalid);
            }
            using var scope = scopes.Enter(proof.TenantId);
            var preview = await previews.BuildAsync(proof, tokens.Hash(proof.Secret), ct);
            if (preview.ExpiresAtUtc is { } expiry && expiry > timeProvider.GetUtcNow().UtcDateTime)
                flow.Remember(new InvitationContinuation(token!, previous is not null && previous.Token == token
                    ? previous.Nonce : tokens.GenerateSecret(), expiry));
            else flow.ClearContinuation();
            return preview;
        });
}
