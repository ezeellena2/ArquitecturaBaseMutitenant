using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Ordena cuenta antes de destino, igual que reauth/gestión, y revalida su dueño.</summary>
internal sealed class LoginAccountGuard(IUserLookup lookup, IUserRepository users, ILoginMethodRepository methods)
{
    internal async Task<Guid?> LockAsync(LoginCodeDestination destination, CancellationToken ct)
    {
        var id = await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email, destination.Value, ct);
        if (id is { } userId) await methods.LockUserAsync(userId, ct);
        return id;
    }
    internal async Task<bool> StillOwnsAsync(Guid? id, LoginCodeDestination destination, CancellationToken ct) =>
        id == await lookup.FindVerifiedUserIdAsync(LoginMethodType.Email, destination.Value, ct);
    internal Task<UserAccountRow?> ReadAsync(Guid id, CancellationToken ct) => users.GetByIdAsync(id, ct);
}
