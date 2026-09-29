using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Aplica la política de alta y encola el código en la transacción del llamador.</summary>
internal sealed class SignupCodeRequester(SignupPolicy policy, LoginCodeIssuer issuer,
    ILoginCodeRepository codes, UserCultures userCultures, IEnumerable<ILoginCodeChannel> channels)
{
    internal async Task<Result<RequestLoginCodeResponse>> RequestAsync(SignupRequest request,
        CancellationToken cancellationToken)
    {
        var destination = LoginCodeDestination.ForEmail(request.Email!);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var allowed = await policy.CanRegisterAsync(cancellationToken);
        if (allowed.IsFailure) return allowed.Error;

        var issued = await issuer.IssueSignupCodeAsync(destination, cancellationToken);
        if (issued.IsFailure) return issued.Error;

        var culture = await userCultures.ResolveAsync(null, request.Culture, cancellationToken);
        var channel = channels.SingleOrDefault(value => value.Key == destination.Channel)
            ?? throw new InvalidOperationException("The email signup channel is not registered.");
        channel.EnqueueSignup(destination.Value, issued.Value.Code, issued.Value.LifetimeMinutes, culture);
        issued.Value.LoginCode.MarkSent(issued.Value.IssuedAtUtc);
        return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
    }
}
