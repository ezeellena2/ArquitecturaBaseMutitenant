using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara las preferencias y el registro Google, sin dar consentimiento implícito al ingreso.</summary>
internal sealed class GoogleAccountRegistrar(SignupPolicy policy, IPersonalSpaceProvisioner personalSpaces,
    AccountRegistrar accounts, LegalConsentRecorder consent, TimeProvider clock)
{
    internal Task<PersonalSpaceDraft> PrepareAsync(string? culture, string? timeZoneId, CancellationToken ct) =>
        personalSpaces.PrepareAsync(culture, timeZoneId, ct);

    internal async Task<Result<Guid>> RegisterAsync(ExternalLogin login, PersonalSpaceDraft draft, CancellationToken ct)
    {
        var allowed = await policy.CanRegisterAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        var account = await accounts.CreateAsync(draft.Culture, draft.TimeZoneId, ct);
        await accounts.StagePersonalAsync(account.Id, draft, ct);
        await consent.RecordCurrentAsync(account.Id, clock.GetUtcNow().UtcDateTime, ct);
        return account.Id;
    }

    internal async Task<Result<Guid>> RegisterBusinessAsync(ExternalLogin login, PersonalSpaceDraft defaults, CancellationToken ct)
    {
        var allowed = await policy.CanRegisterBusinessAsync(ct);
        if (allowed.IsFailure) return allowed.Error;
        var account = await accounts.CreateAsync(defaults.Culture, defaults.TimeZoneId, ct);
        await consent.RecordCurrentAsync(account.Id, clock.GetUtcNow().UtcDateTime, ct);
        return account.Id;
    }
}
