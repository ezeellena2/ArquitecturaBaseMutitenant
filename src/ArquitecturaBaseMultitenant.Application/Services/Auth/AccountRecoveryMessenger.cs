using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Mensajes de «Recuperar mi cuenta», dentro de la UoW del servicio. Lo que va al método nuevo sale en la cultura por
/// defecto y sin cuenta: usar la de la cuenta encontrada delataría que existe. El aviso al dueño de un correo ocupado
/// sale en su cultura.
/// </summary>
internal sealed class AccountRecoveryMessenger(IEnumerable<ILoginCodeChannel> codeChannels,
    IEnumerable<IAccountNoticeChannel> noticeChannels, UserCultures cultures, IUserRepository users)
{
    public async Task SendCodeAsync(LoginCodeDestination destination, IssuedLoginCode issued,
        CancellationToken cancellationToken)
    {
        var channel = codeChannels.SingleOrDefault(value => value.Key == destination.Channel)
            ?? throw new InvalidOperationException("The email login channel is not registered.");
        channel.EnqueueVerification(null, destination.Value, issued.Code, issued.LifetimeMinutes,
            await cultures.ResolveAsync(null, null, cancellationToken));
        issued.LoginCode.MarkSent(issued.IssuedAtUtc);
    }

    public async Task NotifyMethodInUseAsync(Guid ownerId, string email, CancellationToken cancellationToken)
    {
        var owner = await users.GetByIdAsync(ownerId, cancellationToken);
        await EmailChannel().EnqueueAsync(ownerId, email, new AccountNotice.RecoveryMethodInUse(),
            await cultures.ResolveAsync(owner?.Culture, null, cancellationToken), cancellationToken);
    }

    public async Task NotifyReceivedAsync(string email, CancellationToken cancellationToken) =>
        await EmailChannel().EnqueueAsync(null, email, new AccountNotice.RecoveryReceived(),
            await cultures.ResolveAsync(null, null, cancellationToken), cancellationToken);

    private IAccountNoticeChannel EmailChannel() =>
        noticeChannels.FirstOrDefault(channel => channel.MethodType == LoginMethodType.Email)
        ?? throw new InvalidOperationException("The email notice channel is not registered.");
}
