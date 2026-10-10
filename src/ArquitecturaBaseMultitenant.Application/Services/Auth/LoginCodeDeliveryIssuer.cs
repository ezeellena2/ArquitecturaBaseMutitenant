using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Renderiza y encola el código emitido, respetando estado, bloqueo y disponibilidad del método.</summary>
internal sealed class LoginCodeDeliveryIssuer(IUserRepository users, ILoginMethodReader methods,
    LoginMethodAvailabilityPolicy availability, IEnumerable<ILoginCodeChannel> channels,
    UserCultureResolver cultures, ISignInService signIn, LoginCodeIssuer issuer)
{
    internal async Task<Result> EnqueueAsync(LoginCodeDestination destination, IssuedLoginCode issued, Guid? userId,
        IReadOnlyCollection<Guid> invitedTenantIds, CancellationToken ct)
    {
        var account = userId is { } id ? await users.GetByIdAsync(id, ct) : null;
        var culture = await cultures.ResolveAsync(account?.Culture, null, ct);
        var channel = channels.SingleOrDefault(value => value.Key == destination.Channel && value.IsAvailable);
        if (channel is null) return LoginChannelErrors.Unavailable;
        var payload = channel.RenderLoginCode(destination.Value, issued.Code, issued.LifetimeMinutes, culture);
        if (userId is { } knownUserId)
        {
            var expired = account?.OperatorInvitationExpiresAtUtc is { } expires && expires <= issued.IssuedAtUtc;
            if (expired || account is not { Status: UserStatus.Active or UserStatus.Suspended or UserStatus.PendingDeletion }
                || await signIn.IsLockedOutAsync(knownUserId, ct))
            {
                await issuer.PersistAsync(destination, issued, ct);
                return Result.Success();
            }
            var ownMethods = await methods.ListByUserIdAsync(knownUserId, ct);
            var method = ownMethods.SingleOrDefault(value => value.Type == LoginDestinationResolver.Type(destination) && value.Value == destination.Value)
                ?? ownMethods.FirstOrDefault(value => value.Type == LoginMethodType.Google && value.VerifiedAtUtc is not null
                    && value.ContactEmail?.Value == destination.Value);
            if (method is null || !await availability.CanReceiveCodeAsync(knownUserId, method, invitedTenantIds, ct))
            {
                await issuer.PersistAsync(destination, issued, ct);
                return Result.Success();
            }
        }
        var admitted = await issuer.AdmitAsync(destination, issued, ct);
        if (admitted.IsFailure) return admitted;
        channel.EnqueueRenderedLoginCode(payload, userId);
        issued.LoginCode.MarkSent(issued.IssuedAtUtc);
        return Result.Success();
    }
}
