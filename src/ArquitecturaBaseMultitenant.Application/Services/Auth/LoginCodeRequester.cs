using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Emite el código para un método verificado; la verificación informa el estado de la cuenta.</summary>
internal sealed class LoginCodeRequester(
    LoginCodeIssuer issuer,
    IUserLookup userLookup,
    IUserRepository users,
    IEnumerable<ILoginCodeChannel> channels,
    UserCultures userCultures,
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
        if (userId is { } id)
        {
            var account = await users.GetByIdAsync(id, cancellationToken);
            if (account is { Status: UserStatus.Active or UserStatus.Suspended }
                && !await signIn.IsLockedOutAsync(id, cancellationToken))
            {
                var culture = await userCultures.ResolveAsync(account.Culture, null, cancellationToken);
                var channel = channels.SingleOrDefault(value => value.Key == destination.Channel)
                    ?? throw new InvalidOperationException("The email login channel is not registered.");
                channel.Enqueue(destination.Value, issued.Value.Code, issued.Value.LifetimeMinutes, culture);
                issued.Value.LoginCode.MarkSent(issued.Value.IssuedAtUtc);
            }
        }

        return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
    }
}
