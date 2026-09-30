using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Dentro de la transacción del servicio, consume la reautenticación y cambia o quita un método sin dejar la cuenta sin alternativa válida.</summary>
internal sealed class LoginMethodChanger(LoginMethodGuard guard, ILoginMethodRepository methods,
    LoginMethodAvailability availability, ReauthTicketConsumer tickets, IUserRepository users, LoginMethodNotifier notifier)
{
    public async Task<Result> ChangeAsync(Guid userId, ChangeLoginMethodRequest request, bool remove, CancellationToken ct)
    {
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        var all = await methods.ListByUserIdAsync(userId, ct);
        var target = all.SingleOrDefault(method => method.Id == request.MethodId);
        if (target is null) return LoginMethodErrors.NotFound;
        var available = await availability.AvailableAsync(all, ct);
        if (remove && LoginMethodAvailability.CheckRemoval(target, available) is { } blocked) return blocked;
        if (!remove && !available.Any(method => method.Id == target.Id)) return LoginMethodErrors.NotVerified;
        var consumed = await tickets.ConsumeAsync(userId, remove ? ReauthAction.RemoveMethod : ReauthAction.MakePrimary,
            target.Id, request.ReauthTicket!, ct);
        if (consumed.IsFailure) return consumed.Error;
        var nextPrimary = !remove ? target : target.IsPrimary
            ? available.Where(method => method.Id != target.Id).OrderBy(method => method.ManagedByTenantId is not null)
                .ThenBy(method => method.Id).First() : null;
        if (nextPrimary is not null)
        {
            await methods.ClearPrimaryAsync(userId, ct);
            nextPrimary.MakePrimary();
            var contact = LoginMethodNotifier.Contact(nextPrimary);
            await users.SetPrimaryContactAsync(userId, nextPrimary.Type == LoginMethodType.Phone || contact is null
                    ? null : Email.Create(contact).Value,
                nextPrimary.Type == LoginMethodType.Phone ? PhoneNumber.Create(nextPrimary.Value).Value : null, ct);
        }
        await notifier.ChangedAsync(account.Value, all, target, remove ? "Removed" : "Primary", ct);
        if (remove) methods.Remove(target);
        return Result.Success();
    }
}
