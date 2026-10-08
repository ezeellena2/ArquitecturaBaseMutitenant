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
    IUserRepository users, IUserLookup lookup, LoginMethodDomainLinker emailDomains)
{
    internal Task LockEmailAsync(Email email, CancellationToken ct) => identities.LockEmailAsync(email, ct);

    internal async Task<Result> VerifyAsync(LoginMethod method, DateTime nowUtc, CancellationToken ct,
        bool replacePrimary = false, bool assignDomain = true)
    {
        var email = method.Type == LoginMethodType.Email ? Email.Create(method.Value).Value : method.ContactEmail;
        if (email is not null && (await lookup.FindVerifiedUsersByEmailAsync(email, ct)).Any(id => id != method.UserId))
            return method.Type == LoginMethodType.Google ? GoogleMethodErrors.AlreadyUsed : LoginMethodErrors.AlreadyUsed;
        var owner = await lookup.FindMethodAsync(method.Type, method.Value, ct);
        if (owner is { VerifiedAtUtc: not null } && owner.UserId != method.UserId)
            return LoginMethodErrors.AlreadyUsed;
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
        return Result.Success();
    }

    internal Task SynchronizePrimaryAsync(LoginMethod method, CancellationToken ct) =>
        users.SetPrimaryContactAsync(method.UserId,
            method.Type == LoginMethodType.Email ? Email.Create(method.Value).Value : method.ContactEmail,
            method.Type == LoginMethodType.Phone ? PhoneNumber.Create(method.Value).Value : null, ct);
}
