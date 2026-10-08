using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
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
    TimeProvider clock, LoginDestinationResolver destinations, LoginMethodLinker linker,
    ILoginMethodRepository methods)
{
    internal Result<LoginCodeDestination> Resolve(Email? email,
        PhoneInputRequest? phone, string? channel) => destinations.Resolve(email, phone, channel);

    internal async Task<Result<RequestLoginCodeResponse>> RequestAsync(
        RequestLoginCodeRequest request, LoginCodeDestination destination, CancellationToken cancellationToken)
    {
        await linker.LockDestinationAsync(destination, cancellationToken);
        var pending = request.Email is null ? [] : await invitations.ListPendingAsync(request.Email, clock.GetUtcNow().UtcDateTime, cancellationToken);
        var userId = await FindOwnerAsync(destination, request.Email, pending.Count > 0, cancellationToken);
        if (userId is null && pending.Count == 0)
            return FieldErrors.On(request.Email is null ? LoginCodeErrors.PhoneAccountNotFound : LoginCodeErrors.AccountNotFound, LoginDestinationResolver.Field(destination));
        if (userId is { } knownUserId)
        {
            await methods.LockUserAsync(knownUserId, cancellationToken);
            if (await FindOwnerAsync(destination, request.Email, pending.Count > 0, cancellationToken) != knownUserId)
                return FieldErrors.On(request.Email is null ? LoginCodeErrors.PhoneAccountNotFound : LoginCodeErrors.AccountNotFound, LoginDestinationResolver.Field(destination));
        }

        var ownerId = LoginDestinationResolver.Type(destination) == LoginMethodType.Phone ? userId : null;
        var issued = await issuer.IssueLoginCodeAsync(destination, ownerId, cancellationToken);
        if (issued.IsFailure) return issued.Error;
        await delivery.EnqueueAsync(destination, issued.Value, userId, pending.Select(row => row.TenantId).ToArray(), cancellationToken);

        return new RequestLoginCodeResponse(issued.Value.ResendCooldownSeconds);
    }

    private async Task<Guid?> FindOwnerAsync(LoginCodeDestination destination, Email? email, bool invited, CancellationToken ct)
    {
        var userId = await userLookup.FindVerifiedUserIdAsync(LoginDestinationResolver.Type(destination), destination.Value, ct);
        if (userId is not null || !invited || email is null) return userId;
        var owners = await userLookup.FindVerifiedUsersByEmailAsync(email, ct);
        return owners.Count == 1 ? owners[0] : null;
    }
}
