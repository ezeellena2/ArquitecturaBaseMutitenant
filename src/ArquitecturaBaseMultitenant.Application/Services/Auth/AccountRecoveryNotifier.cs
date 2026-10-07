using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Mensajes de «Recuperar mi cuenta», dentro de la UoW del servicio. Lo que va al método nuevo sale en la cultura por
/// defecto y sin cuenta: usar la de la cuenta encontrada delataría que existe. El aviso al dueño de un correo ocupado
/// sale en su cultura, y el aviso a la cuenta encontrada va a sus métodos que siguen andando.
/// </summary>
internal sealed class AccountRecoveryNotifier(IEnumerable<ILoginCodeChannel> codeChannels,
    IEnumerable<IAccountNoticeChannel> noticeChannels, UserCultureResolver cultures, IUserRepository users,
    ILoginMethodRepository methods, LoginMethodAvailabilityPolicy availability, AccountNoticeIssuer notices,
    IPlatformSettingsReader settings)
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

    public async Task NotifyReceivedAsync(string email, CancellationToken cancellationToken) =>
        await EmailChannel().EnqueueAsync(null, email, new AccountNotice.RecoveryReceived(),
            await cultures.ResolveAsync(null, null, cancellationToken), cancellationToken);

    /// <summary>A la cuenta encontrada por el método de antes: alguien pidió recuperarla (lienzo v89, Mensajes).</summary>
    public async Task NotifyAccountAsync(Guid userId, DateTime requestedAtUtc, CancellationToken cancellationToken)
    {
        var account = await users.GetByIdAsync(userId, cancellationToken);
        if (account is null) return;
        var available = await availability.AvailableAsync(
            await methods.ListByUserIdAsync(userId, cancellationToken), cancellationToken);
        var support = (await settings.FindAsync(cancellationToken))?.SupportEmail;
        await notices.EnqueueAsync(userId, available,
            new AccountNotice.RecoveryRequested(requestedAtUtc, account.TimeZoneId)
                { RecipientName = account.DisplayName, SupportEmail = support },
            account.Culture, principalOnly: false, cancellationToken);
    }

    private IAccountNoticeChannel EmailChannel() =>
        noticeChannels.FirstOrDefault(channel => channel.MethodType == LoginMethodType.Email)
        ?? throw new InvalidOperationException("The email notice channel is not registered.");
}
