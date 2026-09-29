using System.Text;
using ArquitecturaBaseMultitenant.Application.Common.Text;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Prepara y crea la identidad Personal Google con ambos consentimientos vigentes.</summary>
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
        var userId = (await users.CreateAsync(FitExternalDisplayName(login.DisplayName),
            draft.Culture, draft.TimeZoneId, cancellationToken)).Id;
        personalSpaces.Stage(draft, userId);
        legal.AddAcceptance(LegalAcceptance.Create(userId, terms, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
        legal.AddAcceptance(LegalAcceptance.Create(userId, privacy, nowUtc,
            requestInfo.IpAddress, requestInfo.UserAgent));
        return userId;
    }

    private static string? FitExternalDisplayName(string? externalName)
    {
        var clean = TextNormalizer.CleanName(externalName);
        if (clean is null || clean.Length <= TextLimits.PersonName) return clean;

        var result = new StringBuilder(TextLimits.PersonName);
        foreach (var rune in clean.EnumerateRunes())
        {
            if (result.Length + rune.Utf16SequenceLength > TextLimits.PersonName) break;
            result.Append(rune);
        }
        return result.ToString().TrimEnd();
    }
}
