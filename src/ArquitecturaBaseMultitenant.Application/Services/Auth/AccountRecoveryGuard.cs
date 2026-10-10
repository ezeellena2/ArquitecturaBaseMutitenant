using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Bloquea identidad → cuenta → código y relee propiedad. El método perdido no exige canal ni país de alta.</summary>
internal sealed class AccountRecoveryGuard(IUserLookup lookup, ILoginMethodRepository methods,
    LoginMethodLinker linker, LoginCodeIssuer codes, LoginDestinationResolver destinations, IPhoneNumberParser phones)
{
    internal Result<(LoginMethodType Type, string Value)> Previous(Email? email, PhoneInputRequest? phone)
    {
        if (email is not null) return Result.Success((LoginMethodType.Email, email.Value));
        var parsed = phones.Parse(phone?.Country, phone?.Number, PhoneUsage.Mobile);
        return parsed.IsFailure ? FieldErrors.On(parsed.Error, "previousPhone")
            : Result.Success((LoginMethodType.Phone, parsed.Value.Value));
    }

    internal Task<Guid?> FindPreviousAsync((LoginMethodType Type, string Value) previous, CancellationToken ct) =>
        lookup.FindVerifiedUserIdAsync(previous.Type, previous.Value, ct);

    internal static Error PreviousNotFound(LoginMethodType type) => type == LoginMethodType.Phone
        ? FieldErrors.On(AccountRecoveryErrors.PhoneAccountNotFound, "previousPhone")
        : FieldErrors.On(AccountRecoveryErrors.AccountNotFound, "previousEmail");

    internal async Task LockAsync(LoginCodeDestination destination, Guid? userId, CancellationToken ct)
    {
        await linker.LockDestinationAsync(destination, ct);
        if (userId is { } accountId) await methods.LockUserAsync(accountId, ct);
        await codes.LockDestinationAsync(destination, ct);
    }

    internal async Task<Result> CheckNewAsync(LoginCodeDestination destination, CancellationToken ct,
        Guid? recoveringUserId = null)
    {
        var valid = destinations.ValidateNew(destination);
        if (valid.IsFailure) return valid.Error;
        var owners = LoginDestinationResolver.Type(destination) == LoginMethodType.Email
            ? await lookup.FindVerifiedUsersByEmailAsync(Email.Create(destination.Value).Value, ct)
            : await lookup.FindVerifiedUserIdAsync(LoginMethodType.Phone, destination.Value, ct) is { } owner
                ? [owner] : Array.Empty<Guid>();
        if (recoveringUserId is { } userId && owners.Contains(userId))
            return AccountRecoveryErrors.MethodAlreadyOnAccount;
        if (owners.Count == 0) return Result.Success();
        if (recoveringUserId is not null) return AccountRecoveryErrors.MethodInUse;
        return LoginDestinationResolver.Type(destination) == LoginMethodType.Phone
            ? FieldErrors.On(AccountRecoveryErrors.PhoneMethodInUse, "phone")
            : FieldErrors.On(AccountRecoveryErrors.MethodInUse, "email");
    }

    internal Task<LoginMethodLookup?> FindPendingAsync(LoginCodeDestination destination, CancellationToken ct) =>
        lookup.FindMethodAsync(LoginDestinationResolver.Type(destination), destination.Value, ct);
}
