using System.Text.Json;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Messaging;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

internal sealed class EmailAccountNoticeChannel(IEmailTemplateRenderer templates, IOutbox outbox) : IAccountNoticeChannel
{
    public string Key => OutboxChannel.Email;
    public LoginMethodType MethodType => LoginMethodType.Email;

    public async Task EnqueueAsync(Guid userId, string destination, AccountNotice notice, CultureProfile culture,
        CancellationToken cancellationToken) => outbox.Enqueue(Key, JsonSerializer.Serialize(
            await templates.RenderAccountNoticeAsync(destination, notice, culture, cancellationToken)), userId);
}
