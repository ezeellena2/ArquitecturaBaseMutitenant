using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara el registro Google personal o empresarial con ambos consentimientos vigentes.</summary>
internal sealed class GoogleAccountRegistrar(
    SignupPolicy policy,
    IPersonalSpaceProvisioner personalSpaces,
    IUserRepository users,
    ILegalRepository legal,
    IRequestInfo requestInfo,
    TimeProvider timeProvider)
{
    internal Task<PersonalSpaceDraft> PrepareAsync(string? culture, string? timeZoneId,
        CancellationToken cancellationToken) =>
        personalSpaces.PrepareAsync(culture, timeZoneId, cancellationToken);

    internal async Task<Result<Guid>> RegisterAsync(ExternalLogin login, PersonalSpaceDraft draft,
        CancellationToken cancellationToken)
    {
        var allowed = await policy.CanRegisterAsync(cancellationToken);
        if (allowed.IsFailure) return allowed.Error;

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var terms = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Terms document has not been seeded.");
        var privacy = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Privacy, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Privacy document has not been seeded.");
        var userId = (await users.CreateAsync(null,
            draft.Culture, draft.TimeZoneId, cancellationToken)).Id;
        personalSpaces.Stage(draft, userId);
        legal.AddAcceptance(LegalAcceptance.Create(userId, terms, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
        legal.AddAcceptance(LegalAcceptance.Create(userId, privacy, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
        return userId;
    }

    internal async Task<Result<Guid>> RegisterBusinessAsync(ExternalLogin login, PersonalSpaceDraft defaults,
        CancellationToken cancellationToken)
    {
        var allowed = await policy.CanRegisterBusinessAsync(cancellationToken);
        if (allowed.IsFailure) return allowed.Error;

        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var terms = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Terms document has not been seeded.");
        var privacy = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Privacy, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Privacy document has not been seeded.");
        var userId = (await users.CreateAsync(null,
            defaults.Culture, defaults.TimeZoneId, cancellationToken)).Id;
        legal.AddAcceptance(LegalAcceptance.Create(userId, terms, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
        legal.AddAcceptance(LegalAcceptance.Create(userId, privacy, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
        return userId;
    }

}
