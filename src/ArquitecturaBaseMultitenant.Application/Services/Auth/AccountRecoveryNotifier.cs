using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>
/// Mensajes de «Recuperar mi cuenta», dentro de la UoW del servicio. Lo que va al método nuevo sale en la cultura por
/// defecto y sin cuenta. Un destino ocupado no recibe avisos; la cuenta del pedido registrado los recibe por sus
/// métodos que siguen andando.
/// </summary>
internal sealed class AccountRecoveryNotifier(IEnumerable<ILoginCodeChannel> codeChannels,
    IEnumerable<IAccountNoticeChannel> noticeChannels, UserCultureResolver cultures, IUserRepository users,
    ILoginMethodRepository methods, LoginMethodAvailabilityPolicy availability, AccountNoticeIssuer notices,
    IPlatformSettingsReader settings)
{
    public async Task<Result> SendCodeAsync(LoginCodeDestination destination, IssuedLoginCode issued,
        CancellationToken cancellationToken)
    {
        var channel = codeChannels.SingleOrDefault(value => value.Key == destination.Channel && value.IsAvailable);
        if (channel is null) return LoginChannelErrors.Unavailable;
        channel.EnqueueVerification(null, destination.Value, issued.Code, issued.LifetimeMinutes,
            await cultures.ResolveAsync(null, null, cancellationToken));
        issued.LoginCode.MarkSent(issued.IssuedAtUtc);
        return Result.Success();
    }

    public async Task NotifyReceivedAsync(LoginCodeDestination destination, CancellationToken cancellationToken) =>
        await NoticeChannel(LoginDestinationResolver.Type(destination)).EnqueueAsync(null, destination.Value, new AccountNotice.RecoveryReceived(),
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

    private IAccountNoticeChannel NoticeChannel(LoginMethodType type) =>
        noticeChannels.FirstOrDefault(channel => channel.MethodType == type)
        ?? throw new InvalidOperationException("The recovery notice channel is not registered.");
}
