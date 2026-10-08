using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Dentro de la transacción del servicio, cambia o quita un método sin dejar la cuenta sin alternativa válida.</summary>
internal sealed class LoginMethodEditor(LoginMethodGuard guard, ILoginMethodRepository methods,
    LoginMethodAvailabilityPolicy availability, LoginMethodLinker linker, LoginMethodNotifier notifier)
{
    public async Task<Result> ChangeAsync(Guid userId, ChangeLoginMethodRequest request, bool remove, CancellationToken ct)
    {
        if (remove)
        {
            var hint = await methods.GetByIdForUserAsync(userId, request.MethodId, ct);
            if (hint is not null && hint.Type is LoginMethodType.Phone or LoginMethodType.Email) await linker.LockMethodAsync(hint, ct);
        }
        var account = await guard.LockAccountAsync(userId, ct);
        if (account.IsFailure) return account.Error;
        var all = await methods.ListByUserIdAsync(userId, ct);
        var target = all.SingleOrDefault(method => method.Id == request.MethodId);
        if (target is null) return LoginMethodErrors.NotFound;
        var available = await availability.AvailableAsync(all, ct);
        if (remove && LoginMethodAvailabilityPolicy.CheckRemoval(target, available) is { } blocked) return blocked;
        if (!remove && !available.Any(method => method.Id == target.Id)) return LoginMethodErrors.NotVerified;
        var nextPrimary = !remove ? target : target.IsPrimary
            ? available.Where(method => method.Id != target.Id).OrderBy(method => method.ManagedByTenantId is not null)
                .ThenBy(method => method.Id).First() : null;
        if (nextPrimary is not null)
        {
            await methods.ClearPrimaryAsync(userId, ct);
            nextPrimary.MakePrimary();
            await linker.SynchronizePrimaryAsync(nextPrimary, ct);
        }
        await notifier.ChangedAsync(account.Value, all, target, remove ? LoginMethodChange.Removed : LoginMethodChange.Primary, ct);
        if (remove)
        {
            await linker.RemovedAsync(target, ct);
            methods.Remove(target);
        }
        return Result.Success();
    }
}
