using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Crea identidades por correo y sus consentimientos; solo el alta Personal crea un espacio Personal.</summary>
internal sealed class SignupAccountRegistrar(IUserRepository users, ILoginMethodRepository methods,
    ILegalRepository legal, IPersonalSpaceProvisioner personalSpaces, IRequestInfo requestInfo,
    LoginMethodDomainLinker emailDomains)
{
    internal async Task<(LegalDocument Terms, LegalDocument Privacy)> GetCurrentLegalAsync(
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        var terms = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Terms document has not been seeded.");
        var privacy = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Privacy, nowUtc, cancellationToken)
            ?? throw new InvalidOperationException("The current Privacy document has not been seeded.");
        return (terms, privacy);
    }

    internal async Task<Guid> RegisterAsync(Email email, PersonalSpaceDraft draft, DateTime nowUtc,
        LegalDocument terms, LegalDocument privacy, CancellationToken cancellationToken)
    {
        var userId = (await users.CreateAsync(null, draft.Culture, draft.TimeZoneId, cancellationToken)).Id;
        var loginMethod = LoginMethod.CreateEmail(userId, email);
        loginMethod.Verify(nowUtc);
        await emailDomains.AssignIfVerifiedAsync(loginMethod, email, cancellationToken);
        if (loginMethod.MakePrimary().IsFailure)
            throw new InvalidOperationException("A verified signup method could not become primary.");
        methods.Add(loginMethod);
        await users.SetPrimaryEmailAsync(userId, email, cancellationToken);
        personalSpaces.Stage(draft, userId);
        await AcceptCurrentLegalAsync(userId, terms, privacy, nowUtc, cancellationToken);
        return userId;
    }

    internal async Task<Guid> RegisterBusinessAsync(Email email, PersonalSpaceDraft defaults, DateTime nowUtc,
        LegalDocument terms, LegalDocument privacy, CancellationToken cancellationToken)
    {
        var userId = (await users.CreateAsync(null, defaults.Culture, defaults.TimeZoneId, cancellationToken)).Id;
        var loginMethod = LoginMethod.CreateEmail(userId, email);
        loginMethod.Verify(nowUtc);
        await emailDomains.AssignIfVerifiedAsync(loginMethod, email, cancellationToken);
        if (loginMethod.MakePrimary().IsFailure)
            throw new InvalidOperationException("A verified signup method could not become primary.");
        methods.Add(loginMethod);
        await users.SetPrimaryEmailAsync(userId, email, cancellationToken);
        await AcceptCurrentLegalAsync(userId, terms, privacy, nowUtc, cancellationToken);
        return userId;
    }

    internal async Task AcceptCurrentLegalAsync(Guid userId, LegalDocument terms, LegalDocument privacy,
        DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (!await legal.HasAcceptedAsync(userId, terms.Id, cancellationToken))
            legal.AddAcceptance(LegalAcceptance.Create(userId, terms, nowUtc,
                requestInfo.IpAddress, requestInfo.UserAgent));
        if (!await legal.HasAcceptedAsync(userId, privacy.Id, cancellationToken))
            legal.AddAcceptance(LegalAcceptance.Create(userId, privacy, nowUtc,
                requestInfo.IpAddress, requestInfo.UserAgent));
    }
}
