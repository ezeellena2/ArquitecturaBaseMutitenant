using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>
/// Vínculo verificado y copia del principal en la UoW del llamador. Orden: identidad externa (correo/subject),
/// cuenta, destino del código; se toman antes de releer propiedad. Nunca abre una transacción.
/// </summary>
internal sealed class LoginMethodLinker(IExternalLoginLock identities, ILoginMethodRepository methods,
    IUserRepository users, IUserLookup lookup, LoginMethodDomainLinker emailDomains, IEnumerable<IPhoneLinkObserver>? phoneObservers = null)
{
    internal Task LockDestinationAsync(LoginCodeDestination destination, CancellationToken ct) =>
        destination.Channel == LoginCodeChannel.Email ? identities.LockEmailAsync(Email.Create(destination.Value).Value, ct)
            : identities.LockPhoneAsync(PhoneNumber.Create(destination.Value).Value, ct);

    internal Task LockMethodAsync(LoginMethod method, CancellationToken ct) => method.Type == LoginMethodType.Phone
        ? identities.LockPhoneAsync(PhoneNumber.Create(method.Value).Value, ct)
        : identities.LockEmailAsync(Email.Create(method.Type == LoginMethodType.Email ? method.Value : method.ContactEmail!.Value).Value, ct);

    internal async Task RemovedAsync(LoginMethod method, CancellationToken ct)
    {
        if (method.Type == LoginMethodType.Phone)
            foreach (var observer in phoneObservers ?? []) await observer.RemovedAsync(method.UserId, method.Id, PhoneNumber.Create(method.Value).Value, ct);
    }

    internal Task LockEmailAsync(Email email, CancellationToken ct) => identities.LockEmailAsync(email, ct);

    internal async Task<Result> VerifyAsync(LoginMethod method, DateTime nowUtc, CancellationToken ct,
        bool replacePrimary = false, bool assignDomain = true)
    {
        var email = method.Type == LoginMethodType.Email ? Email.Create(method.Value).Value : method.ContactEmail;
        if (email is not null && (await lookup.FindVerifiedUsersByEmailAsync(email, ct)).Any(id => id != method.UserId))
            return method.Type == LoginMethodType.Google ? GoogleMethodErrors.AlreadyUsed : LoginMethodErrors.AlreadyUsed;
        var owner = await lookup.FindMethodAsync(method.Type, method.Value, ct);
        if (owner is { VerifiedAtUtc: not null } && owner.UserId != method.UserId)
            return method.Type == LoginMethodType.Phone ? LoginMethodErrors.PhoneAlreadyUsed : LoginMethodErrors.AlreadyUsed;
        var wasVerified = method.VerifiedAtUtc is not null;
        method.Verify(nowUtc);
        if (assignDomain) await emailDomains.AssignIfVerifiedAsync(method, email, ct);
        var all = await methods.ListByUserIdAsync(method.UserId, ct);
        if (replacePrimary) await methods.ClearPrimaryAsync(method.UserId, ct);
        if (replacePrimary || !all.Any(value => value.IsPrimary))
        {
            var primary = method.MakePrimary();
            if (primary.IsFailure) return primary.Error;
            await SynchronizePrimaryAsync(method, ct);
        }
        if (method.Type == LoginMethodType.Phone && !wasVerified)
            foreach (var observer in phoneObservers ?? []) await observer.VerifiedAsync(method.UserId, method.Id, PhoneNumber.Create(method.Value).Value, ct);
        return Result.Success();
    }

    internal Task SynchronizePrimaryAsync(LoginMethod method, CancellationToken ct) =>
        users.SetPrimaryContactAsync(method.UserId,
            method.Type == LoginMethodType.Email ? Email.Create(method.Value).Value : method.ContactEmail,
            method.Type == LoginMethodType.Phone ? PhoneNumber.Create(method.Value).Value : null, ct);
}
