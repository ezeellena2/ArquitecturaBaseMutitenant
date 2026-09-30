using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

internal sealed class LoginMethodAvailability(IEnumerable<ILoginCodeChannel> channels,
    IGoogleAvailability google, ILoginMethodMembershipReader memberships)
{
    public async Task<IReadOnlyList<LoginMethod>> AvailableAsync(IReadOnlyList<LoginMethod> methods, CancellationToken ct)
    {
        var result = new List<LoginMethod>();
        foreach (var method in methods)
        {
            var channelAvailable = method.Type switch
            {
                LoginMethodType.Email => channels.Any(channel => channel.Key == LoginCodeChannel.Email),
                LoginMethodType.Google => google.IsEnabled,
                _ => false,
            };
            var managedActive = method.ManagedByTenantId is not { } tenantId ||
                await memberships.IsActiveAsync(method.UserId, tenantId, ct);
            if (method.CanSignIn(channelAvailable, managedActive)) result.Add(method);
        }
        return result;
    }

    public bool CanReceiveCode(LoginMethod method) => method.Type is LoginMethodType.Email or LoginMethodType.Google
        && LoginMethodNotifier.Contact(method) is not null && channels.Any(channel => channel.Key == LoginCodeChannel.Email);

    public bool CanReceiveCode(LoginMethodRow method) => (method.Type == LoginMethodType.Email
        || (method.Type == LoginMethodType.Google && method.ContactEmail is not null))
        && channels.Any(channel => channel.Key == LoginCodeChannel.Email);

    public async Task<bool> IsAvailableAsync(Guid userId, LoginMethodRow method, CancellationToken ct)
    {
        var channelAvailable = method.Type switch
        {
            LoginMethodType.Email => channels.Any(channel => channel.Key == LoginCodeChannel.Email),
            LoginMethodType.Google => google.IsEnabled,
            _ => false,
        };
        return method.VerifiedAtUtc is not null && channelAvailable
            && (method.ManagedByTenantId is not { } tenantId || await memberships.IsActiveAsync(userId, tenantId, ct));
    }

    public static Error? CheckRemoval(LoginMethod target, IReadOnlyList<LoginMethod> available)
    {
        var remaining = available.Where(method => method.Id != target.Id).ToArray();
        if (remaining.Length == 0) return LoginMethodErrors.LastMethod;
        return target.ManagedByTenantId is null && available.Any(method => method.Id == target.Id)
            && remaining.All(method => method.ManagedByTenantId is not null) ? LoginMethodErrors.LastPersonalMethod : null;
    }
}
