using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Crea identidad, método principal, Personal y consentimientos en la transacción vigente.</summary>
internal sealed class SignupAccountRegistrar(IUserRepository users, ILoginMethodRepository methods,
    ILegalRepository legal, IPersonalSpaceProvisioner personalSpaces, IRequestInfo requestInfo)
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
        if (loginMethod.MakePrimary().IsFailure)
            throw new InvalidOperationException("A verified signup method could not become primary.");
        methods.Add(loginMethod);
        await users.SetPrimaryEmailAsync(userId, email, cancellationToken);
        personalSpaces.Stage(draft, userId);
        legal.AddAcceptance(LegalAcceptance.Create(userId, terms, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
        legal.AddAcceptance(LegalAcceptance.Create(userId, privacy, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
        return userId;
    }
}
