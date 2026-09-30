using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Comprueba el código de respaldo vinculado a la acción y emite un ticket de uso único para completarla.</summary>
internal sealed class ReauthVerifier(LoginMethodGuard guard, ILoginMethodRepository methods,
    LoginMethodAvailability availability, ILoginCodeRepository codes, ILoginCodeHasher hasher,
    IReauthTicketRepository tickets, ISecureTokenGenerator secrets, TimeProvider timeProvider)
{
    public async Task<Result<ReauthResponse>> VerifyAsync(Guid userId, VerifyReauthRequest request, CancellationToken ct)
    {
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        if (request.SourceMethodId == request.TargetMethodId) return ReauthErrors.OtherMethodRequired;
        var all = await methods.ListByUserIdAsync(userId, ct);
        if (request.TargetMethodId is { } targetId && !all.Any(method => method.Id == targetId))
            return LoginMethodErrors.NotFound;
        var source = (await availability.AvailableAsync(all, ct)).SingleOrDefault(method => method.Id == request.SourceMethodId);
        if (source is null || !availability.CanReceiveCode(source)) return ReauthErrors.Invalid;
        var destination = LoginCodeDestination.ForEmail(Email.Create(LoginMethodNotifier.Contact(source)).Value);
        await codes.LockDestinationAsync(destination, ct);
        var code = await codes.GetLatestAsync(destination, LoginCodePurpose.Reauthenticate, userId, ct);
        if (code is null || !code.MatchesReauthentication(request.Action, source.Id, request.TargetMethodId))
            return ReauthErrors.Invalid;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var verified = code.VerifyFor(userId, LoginCodePurpose.Reauthenticate,
            hasher.Hash(destination, LoginCodePurpose.Reauthenticate, request.Code!), nowUtc);
        if (verified.IsFailure) return verified.Error;
        var secret = secrets.Generate();
        var ticket = ReauthTicket.Issue(userId, request.Action, source.Id, request.TargetMethodId,
            secrets.Hash(secret), nowUtc);
        if (ticket.IsFailure) return ticket.Error;
        tickets.Add(ticket.Value);
        return new ReauthResponse(secret);
    }
}
