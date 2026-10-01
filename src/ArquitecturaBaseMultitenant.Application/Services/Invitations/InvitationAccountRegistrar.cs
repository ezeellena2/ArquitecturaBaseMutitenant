using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

internal sealed class InvitationAccountRegistrar(IUserRepository users, ILoginMethodRepository methods,
    IUserLookup lookup, ILegalRepository legal, IRequestInfo requestInfo, InvitationIdentityDefaults preferences)
{
    internal async Task<Guid> RegisterAsync(Email email, DateTime nowUtc, CancellationToken ct)
    {
        var defaults = await preferences.ReadAsync(ct);
        var terms = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Terms, nowUtc, ct)
            ?? throw new InvalidOperationException("Current Terms are required.");
        var privacy = await legal.GetCurrentDocumentAsync(LegalDocumentKind.Privacy, nowUtc, ct)
            ?? throw new InvalidOperationException("Current Privacy is required.");
        var pending = await FindPendingAsync(email, ct);
        if (pending is not null) methods.Remove(pending);
        var user = await users.CreateAsync(null, defaults.Culture, defaults.TimeZoneId, ct);
        await AddEmailAsync(user.Id, email, nowUtc, makePrimary: true, ct);
        legal.AddAcceptance(LegalAcceptance.Create(user.Id, terms, nowUtc, requestInfo.IpAddress, requestInfo.UserAgent));
        legal.AddAcceptance(LegalAcceptance.Create(user.Id, privacy, nowUtc, requestInfo.IpAddress, requestInfo.UserAgent));
        return user.Id;
    }

    internal async Task EnsureEmailAsync(Guid userId, Email email, DateTime nowUtc, CancellationToken ct)
    {
        var found = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, ct);
        if (found is { VerifiedAtUtc: not null })
        {
            if (found.UserId != userId) throw new InvalidOperationException("The verified owner changed after the destination lock.");
            return;
        }
        var own = await methods.ListByUserIdAsync(userId, ct);
        var makePrimary = !own.Any(method => method.IsPrimary && method.VerifiedAtUtc is not null);
        var pending = await FindPendingAsync(email, ct);
        if (pending is not null) methods.Remove(pending);
        await AddEmailAsync(userId, email, nowUtc, makePrimary, ct);
    }

    private async Task<LoginMethod?> FindPendingAsync(Email email, CancellationToken ct)
    {
        var found = await lookup.FindMethodAsync(LoginMethodType.Email, email.Value, ct);
        return found is { VerifiedAtUtc: null } ? await methods.GetByIdAsync(found.MethodId, ct) : null;
    }

    private async Task AddEmailAsync(Guid userId, Email email, DateTime nowUtc, bool makePrimary, CancellationToken ct)
    {
        var method = LoginMethod.CreateEmail(userId, email);
        method.Verify(nowUtc);
        if (makePrimary && method.MakePrimary().IsFailure) throw new InvalidOperationException("A verified email could not become primary.");
        methods.Add(method);
        if (makePrimary) await users.SetPrimaryEmailAsync(userId, email, ct);
    }
}
