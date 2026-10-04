using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Avisos de la decisión al correo nuevo del pedido, dentro de la UoW. Aprobado sale en la cultura de la cuenta, que
/// ahora tiene ese correo; rechazado, en la cultura por defecto y sin cuenta, para no delatar ninguna.
/// </summary>
internal sealed class PlatformRecoveryNotifier(IEnumerable<IAccountNoticeChannel> channels, UserCultures cultures,
    IPublicOrigin origin)
{
    public async Task ApprovedAsync(UserAccountRow account, Email email, CancellationToken ct)
    {
        var url = origin.Value is { } value ? new Uri(value, "/login").AbsoluteUri : "/login";
        await EmailChannel().EnqueueAsync(account.Id, email.Value, new AccountNotice.RecoveryApproved(url),
            await cultures.ResolveAsync(account.Culture, null, ct), ct);
    }

    public async Task RejectedAsync(string email, CancellationToken ct) =>
        await EmailChannel().EnqueueAsync(null, email, new AccountNotice.RecoveryRejected(),
            await cultures.ResolveAsync(null, null, ct), ct);

    private IAccountNoticeChannel EmailChannel() =>
        channels.FirstOrDefault(channel => channel.MethodType == LoginMethodType.Email)
        ?? throw new InvalidOperationException("The email notice channel is not registered.");
}
