using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Identity;

/// <summary>Encola dentro de la UoW del caso de uso, sin duplicar contactos de Google y correo.</summary>
internal sealed class AccountNoticeIssuer(IEnumerable<IAccountNoticeChannel> channels, UserCultures cultures)
{
    public async Task EnqueueAsync(Guid userId, IReadOnlyList<LoginMethod> methods, AccountNotice notice,
        string culture, bool principalOnly, CancellationToken cancellationToken)
    {
        var profile = await cultures.ResolveAsync(culture, null, cancellationToken);
        var destinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var method in methods.Where(method => method.VerifiedAtUtc is not null &&
                     (!principalOnly || method.IsPrimary)))
        {
            var type = method.Type == LoginMethodType.Google ? LoginMethodType.Email : method.Type;
            var destination = method.Type == LoginMethodType.Google ? method.ContactEmail?.Value : method.Value;
            var channel = channels.FirstOrDefault(channel => channel.MethodType == type);
            if (destination is null || channel is null || !destinations.Add(channel.Key + ":" + destination)) continue;
            await channel.EnqueueAsync(userId, destination, notice, profile, cancellationToken);
        }
    }
}
