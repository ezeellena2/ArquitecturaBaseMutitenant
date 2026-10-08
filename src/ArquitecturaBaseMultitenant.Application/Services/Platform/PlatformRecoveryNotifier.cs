using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Platform;

/// <summary>
/// Avisos de la decisión, dentro de la UoW. Aprobado sale al correo nuevo en la cultura de la cuenta, que ahora tiene
/// ese correo, y el aviso de método agregado va a los demás métodos de la cuenta (ADR 0033): el correo nuevo recibe
/// un solo aviso. Rechazado sale en la cultura por defecto y sin cuenta, para no delatar ninguna.
/// </summary>
internal sealed class PlatformRecoveryNotifier(IEnumerable<IAccountNoticeChannel> channels, UserCultureResolver cultures,
    IPublicOrigin origin, LoginMethodNotifier loginMethods)
{
    public async Task ApprovedAsync(UserAccountRow account, LoginMethod added, IReadOnlyList<LoginMethod> methods,
        CancellationToken ct)
    {
        var url = origin.Value is { } value ? new Uri(value, "/login").AbsoluteUri : "/login";
        await EmailChannel().EnqueueAsync(account.Id, added.Value, new AccountNotice.RecoveryApproved(url),
            await cultures.ResolveAsync(account.Culture, null, ct), ct);
        await loginMethods.ChangedAsync(account, [.. methods.Where(method => method.Id != added.Id)], added, LoginMethodChange.Added, ct);
    }

    public async Task RejectedAsync(string email, CancellationToken ct) =>
        await EmailChannel().EnqueueAsync(null, email, new AccountNotice.RecoveryRejected(),
            await cultures.ResolveAsync(null, null, ct), ct);

    private IAccountNoticeChannel EmailChannel() =>
        channels.FirstOrDefault(channel => channel.MethodType == LoginMethodType.Email)
        ?? throw new InvalidOperationException("The email notice channel is not registered.");
}
