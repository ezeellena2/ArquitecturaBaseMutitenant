using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Emite el código para un método verificado; la verificación informa el estado de la cuenta.</summary>
internal sealed class LoginCodeRequester(
    LoginCodeIssuer issuer,
    IUserLookup userLookup,
    ILoginInvitationReader invitations,
    LoginCodeDeliveryIssuer delivery,
    TimeProvider clock)
{
    internal async Task<Result<RequestLoginCodeResponse>> RequestAsync(
        RequestLoginCodeRequest request, CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        await issuer.LockDestinationAsync(destination, cancellationToken);
        var userId = await userLookup.FindVerifiedUserIdAsync(
            LoginMethodType.Email, destination.Value, cancellationToken);
        var pending = await invitations.ListPendingAsync(request.Email!, clock.GetUtcNow().UtcDateTime, cancellationToken);
        if (userId is null && pending.Count == 0)
            return FieldErrors.On(LoginCodeErrors.AccountNotFound, "email");
        if (userId is null)
        {
            var owners = await userLookup.FindVerifiedUsersByEmailAsync(request.Email!, cancellationToken);
            if (owners.Count == 1) userId = owners[0];
        }

        var issued = await issuer.IssueLoginCodeAsync(destination, cancellationToken);
        if (issued.IsFailure) return issued.Error;
        await delivery.EnqueueAsync(destination, issued.Value, userId, pending.Select(row => row.TenantId).ToArray(), cancellationToken);

        return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
    }
}
