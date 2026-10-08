using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

using ArquitecturaBaseMultitenant.Application.Services.Identity;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

internal sealed class InvitationAccountRegistrar(AccountRegistrar accounts, ILoginMethodRepository methods,
    IUserLookup lookup, LegalConsentRecorder consent, InvitationIdentityDefaultsResolver preferences,
    LoginMethodLinker linker)
{
    internal async Task<Guid> RegisterAsync(Guid organizationId, Email email, DateTime nowUtc, CancellationToken ct)
    {
        var userId = await RegisterVerifiedEmailAsync(organizationId, email, nowUtc, ct);
        await consent.RecordCurrentAsync(userId, nowUtc, ct);
        return userId;
    }

    /// <summary>El OTP prueba el correo, pero no trae consentimiento legal: queda pendiente para el gate existente.</summary>
    internal async Task<Guid> RegisterVerifiedEmailAsync(Guid organizationId, Email email, DateTime nowUtc, CancellationToken ct)
    {
        var defaults = await preferences.ReadAsync(ct);
        var pending = await FindPendingAsync(email, ct);
        if (pending is not null) methods.Remove(pending);
        var user = await accounts.CreateAsync(defaults.Culture, defaults.TimeZoneId, ct);
        await AddEmailAsync(user.Id, email, organizationId, nowUtc, ct);
        return user.Id;
    }

    internal async Task EnsureEmailAsync(Guid userId, Guid organizationId, Email email, DateTime nowUtc, CancellationToken ct)
    {
        var found = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, ct);
        if (found is { VerifiedAtUtc: not null })
        {
            if (found.UserId != userId) throw new InvalidOperationException("The verified owner changed after the destination lock.");
            return;
        }
        var pending = await FindPendingAsync(email, ct);
        if (pending is not null) methods.Remove(pending);
        await AddEmailAsync(userId, email, organizationId, nowUtc, ct);
    }

    private async Task<LoginMethod?> FindPendingAsync(Email email, CancellationToken ct)
    {
        var found = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, ct);
        return found is { VerifiedAtUtc: null } ? await methods.GetByIdAsync(found.MethodId, ct) : null;
    }

    private async Task AddEmailAsync(Guid userId, Email email, Guid organizationId, DateTime nowUtc,
        CancellationToken ct)
    {
        var method = LoginMethod.CreateInvitedEmail(userId, email, organizationId);
        var linked = await linker.VerifyAsync(method, nowUtc, ct);
        if (linked.IsFailure) throw new InvalidOperationException("Invitation ownership changed under the identity lock.");
        methods.Add(method);
    }
}
