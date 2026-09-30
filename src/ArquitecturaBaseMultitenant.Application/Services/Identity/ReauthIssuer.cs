using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

internal sealed class ReauthIssuer(LoginMethodGuard guard, ILoginMethodRepository methods,
    LoginMethodAvailability availability, LoginCodeIssuer codes, IEnumerable<ILoginCodeChannel> channels,
    UserCultures cultures)
{
    public async Task<Result<ReauthCodeResponse>> RequestAsync(Guid userId, RequestReauthRequest request, CancellationToken ct)
    {
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        if (request.Action == ReauthAction.DeleteAccount && AccountDeletionPolicy.CheckRequest(account.Value) is { } blockedState)
            return blockedState;
        var all = await methods.ListByUserIdAsync(userId, ct);
        var available = await availability.AvailableAsync(all, ct);
        if (request.TargetMethodId is { } targetId)
        {
            var target = all.SingleOrDefault(method => method.Id == targetId);
            if (target is null) return LoginMethodErrors.NotFound;
            if (request.Action == ReauthAction.RemoveMethod && LoginMethodAvailability.CheckRemoval(target, available) is { } blocked)
                return blocked;
            if (request.Action == ReauthAction.MakePrimary && !available.Any(method => method.Id == targetId))
                return LoginMethodErrors.NotVerified;
        }
        var source = available.Where(method => method.Id != request.TargetMethodId && availability.CanReceiveCode(method))
            .OrderByDescending(method => method.IsPrimary).ThenBy(method => method.ManagedByTenantId is not null)
            .ThenBy(method => method.Id).FirstOrDefault();
        if (source is null) return ReauthErrors.OtherMethodRequired;
        var destination = LoginCodeDestination.ForEmail(Email.Create(LoginMethodNotifier.Contact(source)).Value);
        var issued = await codes.IssueAccountCodeAsync(destination, LoginCodePurpose.Reauthenticate, userId, ct);
        if (issued.IsFailure) return issued.Error;
        var code = issued.Value;
        code.LoginCode.BindReauthentication(request.Action, source.Id, request.TargetMethodId);
        var channel = channels.Single(channel => channel.Key == destination.Channel);
        channel.EnqueueRenderedLoginCode(channel.RenderLoginCode(destination.Value, code.Code, code.LifetimeMinutes,
            await cultures.ResolveAsync(account.Value.Culture, null, ct)), userId);
        code.LoginCode.MarkSent(code.IssuedAtUtc);
        return new ReauthCodeResponse(source.Id, LoginMethodNotifier.Mask(source), code.ResendCooldownSeconds);
    }
}
