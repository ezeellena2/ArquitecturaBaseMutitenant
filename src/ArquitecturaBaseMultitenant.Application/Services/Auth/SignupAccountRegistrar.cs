using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Alta por correo con consentimiento explícito; solo el registro personal crea Personal.</summary>
internal sealed class SignupAccountRegistrar(AccountRegistrar accounts, LoginMethodLinker linker,
    ILoginMethodRepository methods, LegalConsentRecorder consent)
{
    internal Task<(LegalDocument Terms, LegalDocument Privacy)> GetCurrentLegalAsync(DateTime nowUtc, CancellationToken ct) =>
        consent.GetCurrentAsync(nowUtc, ct);

    internal async Task<Guid> RegisterAsync(Email email, PersonalSpaceDraft draft, DateTime nowUtc,
        LegalDocument terms, LegalDocument privacy, CancellationToken ct)
    {
        var userId = await RegisterBusinessAsync(email, draft, nowUtc, terms, privacy, ct);
        await accounts.StagePersonalAsync(userId, draft, ct);
        return userId;
    }

    internal async Task<Guid> RegisterBusinessAsync(Email email, PersonalSpaceDraft defaults, DateTime nowUtc,
        LegalDocument terms, LegalDocument privacy, CancellationToken ct)
    {
        var account = await accounts.CreateAsync(defaults.Culture, defaults.TimeZoneId, ct);
        var method = LoginMethod.CreateEmail(account.Id, email);
        var linked = await linker.VerifyAsync(method, nowUtc, ct);
        if (linked.IsFailure) throw new InvalidOperationException("Signup ownership changed under the identity lock.");
        methods.Add(method);
        await AcceptCurrentLegalAsync(account.Id, terms, privacy, nowUtc, ct);
        return account.Id;
    }

    internal async Task AcceptCurrentLegalAsync(Guid userId, LegalDocument terms, LegalDocument privacy,
        DateTime nowUtc, CancellationToken ct)
    {
        await consent.RecordAsync(userId, terms, nowUtc, ct);
        await consent.RecordAsync(userId, privacy, nowUtc, ct);
    }
}
