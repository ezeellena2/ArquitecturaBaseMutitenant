using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Decide qué métodos verificados pueden usarse para ingresar o respaldar otro método según canales configurados y membresías activas.</summary>
internal sealed class LoginMethodAvailabilityPolicy(IEnumerable<ILoginCodeChannel> channels,
    IGoogleAvailability google, ILoginMethodMembershipReader memberships)
{
    internal static bool WasVerifiedBeforeSession(LoginMethod method, DateTime? sessionStartedAtUtc) =>
        sessionStartedAtUtc is { Kind: DateTimeKind.Utc } && method.VerifiedAtUtc is { } verifiedAtUtc
        && verifiedAtUtc <= sessionStartedAtUtc;

    public async Task<IReadOnlyList<LoginMethod>> AvailableAsync(IReadOnlyList<LoginMethod> methods, CancellationToken ct)
    {
        var result = new List<LoginMethod>();
        foreach (var method in methods)
        {
            var channelAvailable = HasChannel(method.Type);
            var managedActive = method.ManagedByTenantId is not { } tenantId ||
                await memberships.IsActiveAsync(method.UserId, tenantId, ct);
            if (method.CanSignIn(channelAvailable, managedActive)) result.Add(method);
        }
        return result;
    }

    /// <summary>Tipos de método con canal configurado; Plataforma deriva NeedsRecovery en SQL con esta misma regla.</summary>
    public IReadOnlyCollection<LoginMethodType> SignInTypes() =>
        [.. Enum.GetValues<LoginMethodType>().Where(HasChannel)];

    private bool HasChannel(LoginMethodType type) => type switch
    {
        LoginMethodType.Email => channels.Any(channel => channel.Key == LoginCodeChannel.Email),
        LoginMethodType.Google => google.IsEnabled,
        _ => false,
    };

    public bool CanReceiveCode(LoginMethod method) => method.Type is LoginMethodType.Email or LoginMethodType.Google
        && LoginMethodNotifier.Contact(method) is not null && channels.Any(channel => channel.Key == LoginCodeChannel.Email);

    public bool CanReceiveCode(LoginMethodRow method) => (method.Type == LoginMethodType.Email
        || (method.Type == LoginMethodType.Google && method.ContactEmail is not null))
        && channels.Any(channel => channel.Key == LoginCodeChannel.Email);

    public async Task<bool> IsAvailableAsync(Guid userId, LoginMethodRow method, CancellationToken ct)
    {
        var channelAvailable = HasChannel(method.Type);
        return method.VerifiedAtUtc is not null && channelAvailable
            && (method.ManagedByTenantId is not { } tenantId || await memberships.IsActiveAsync(userId, tenantId, ct));
    }

    public async Task<bool> HasPersonalBackupAsync(Guid userId, IReadOnlyList<LoginMethodRow> methods, CancellationToken ct)
    {
        foreach (var method in methods)
            if (method.IsPersonalBackup && await IsAvailableAsync(userId, method, ct)) return true;
        return false;
    }

    public static Error? CheckRemoval(LoginMethod target, IReadOnlyList<LoginMethod> available)
    {
        var remaining = available.Where(method => method.Id != target.Id).ToArray();
        if (remaining.Length == 0) return LoginMethodErrors.LastMethod;
        return target.IsPersonalBackup && available.Any(method => method.Id == target.Id)
            && remaining.All(method => !method.IsPersonalBackup) ? LoginMethodErrors.LastPersonalMethod : null;
    }
}
