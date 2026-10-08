using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Aplica la política de alta y encola el código en la transacción del llamador.</summary>
internal sealed class SignupCodeRequester(SignupPolicy policy, LoginCodeIssuer issuer,
    ILoginCodeRepository codes, UserCultureResolver userCultures, IEnumerable<ILoginCodeChannel> channels,
    IUserLookup users, LoginDestinationResolver destinations, SignupExistingMethodVerifier existing)
{
    internal Result<LoginCodeDestination> Resolve(Email? email,
        PhoneInputRequest? phone, string? channel) => destinations.Resolve(email, phone, channel);

    internal async Task<Result<RequestLoginCodeResponse>> RequestAsync(SignupRequest request, LoginCodeDestination destination,
        CancellationToken cancellationToken)
    {
        var owner = await existing.LockAsync(destination, cancellationToken);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var allowed = await policy.CanRegisterAsync(cancellationToken);
        if (allowed.IsFailure) return allowed.Error;
        if (owner is { VerifiedAtUtc: not null } || request.Email is not null && (await users.FindVerifiedUsersByEmailAsync(request.Email, cancellationToken)).Count > 0)
            return FieldErrors.On(request.Email is null ? SignupErrors.PhoneTaken : SignupErrors.EmailTaken, LoginDestinationResolver.Field(destination));
        var validNew = destinations.ValidateNew(destination);
        if (validNew.IsFailure) return validNew.Error;

        var issued = await issuer.IssueSignupCodeAsync(destination, cancellationToken);
        if (issued.IsFailure) return issued.Error;

        var culture = await userCultures.ResolveAsync(null, request.Culture, cancellationToken);
        var channel = channels.SingleOrDefault(value => value.Key == destination.Channel)
            ?? throw new InvalidOperationException("The signup channel is not registered.");
        channel.EnqueueSignup(destination.Value, issued.Value.Code, issued.Value.LifetimeMinutes, culture);
        issued.Value.LoginCode.MarkSent(issued.Value.IssuedAtUtc);
        return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
    }

    internal async Task<Result<RequestLoginCodeResponse>> RequestBusinessSignupAsync(SignupRequest request, LoginCodeDestination destination,
        CancellationToken cancellationToken)
    {
        var owner = await existing.LockAsync(destination, cancellationToken);
        await codes.LockDestinationAsync(destination, cancellationToken);
        var allowed = await policy.CanRegisterBusinessAsync(cancellationToken);
        if (allowed.IsFailure) return allowed.Error;

        if (owner is not { VerifiedAtUtc: not null })
        {
            var validNew = destinations.ValidateNew(destination);
            if (validNew.IsFailure) return validNew.Error;
        }
        var ownerId = LoginDestinationResolver.Type(destination) == LoginMethodType.Phone && owner is { VerifiedAtUtc: not null }
            ? (Guid?)owner.UserId : null;
        var issued = await issuer.IssueBusinessSignupCodeAsync(destination, ownerId, cancellationToken);
        if (issued.IsFailure) return issued.Error;

        var culture = await userCultures.ResolveAsync(null, request.Culture, cancellationToken);
        var channel = channels.SingleOrDefault(value => value.Key == destination.Channel)
            ?? throw new InvalidOperationException("The signup channel is not registered.");
        channel.EnqueueSignup(destination.Value, issued.Value.Code, issued.Value.LifetimeMinutes, culture);
        issued.Value.LoginCode.MarkSent(issued.Value.IssuedAtUtc);
        return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
    }
}
