using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Emite el código para un método verificado; la verificación informa el estado de la cuenta.</summary>
internal sealed class LoginCodeRequester(
    LoginCodeIssuer issuer,
    IUserLookup userLookup,
    IUserRepository users,
    ILoginMethodReader loginMethods,
    LoginMethodAvailabilityPolicy availability,
    IEnumerable<ILoginCodeChannel> channels,
    UserCultureResolver userCultures,
    ISignInService signIn)
{
    internal async Task<Result<RequestLoginCodeResponse>> RequestAsync(
        RequestLoginCodeRequest request, CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        var issued = await issuer.IssueLoginCodeAsync(destination, cancellationToken);
        if (issued.IsFailure) return issued.Error;

        var userId = await userLookup.FindVerifiedUserIdAsync(
            LoginMethodType.Email, destination.Value, cancellationToken);
        UserAccountRow? account = null;
        if (userId is { } id)
        {
            account = await users.GetByIdAsync(id, cancellationToken);
        }

        var culture = await userCultures.ResolveAsync(account?.Culture, null, cancellationToken);
        var channel = channels.SingleOrDefault(value => value.Key == destination.Channel)
            ?? throw new InvalidOperationException("The email login channel is not registered.");
        var payload = channel.RenderLoginCode(destination.Value, issued.Value.Code,
            issued.Value.LifetimeMinutes, culture);
        // ADR 0042: con una invitación de operador vencida no se entra; no sale el código, como sin cuenta.
        var expiredInvitation = account?.OperatorInvitationExpiresAtUtc is { } expiresAtUtc
            && expiresAtUtc <= issued.Value.IssuedAtUtc;
        if (userId is { } knownUserId && !expiredInvitation
            && account is { Status: UserStatus.Active or UserStatus.Suspended or UserStatus.PendingDeletion }
            && !await signIn.IsLockedOutAsync(knownUserId, cancellationToken))
        {
            var method = (await loginMethods.ListByUserIdAsync(knownUserId, cancellationToken))
                .SingleOrDefault(value => value.Type == LoginMethodType.Email && value.Value == destination.Value);
            if (method is not null && await availability.IsAvailableAsync(knownUserId, method, cancellationToken))
            {
                channel.EnqueueRenderedLoginCode(payload, knownUserId);
                issued.Value.LoginCode.MarkSent(issued.Value.IssuedAtUtc);
            }
        }

        return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
    }
}
