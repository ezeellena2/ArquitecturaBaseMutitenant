using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;

/// <summary>Lee y consume únicamente el código del destinatario desde el pickup de esta fixture.</summary>
internal static class PickupCodeReader
{
    public static async Task<string> ReadAsync(IServiceProvider provider, string email,
        CancellationToken cancellationToken)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var configured = services.GetRequiredService<IOptions<EmailOptions>>().Value.PickupDirectory;
        var directory = Path.IsPathFullyQualified(configured)
            ? configured
            : Path.Combine(services.GetRequiredService<IHostEnvironment>().ContentRootPath, configured);
        // Cada despacho reclama un mensaje; otros tests de la fixture pueden dejar más de veinte.
        for (var attempt = 0; attempt < 100; attempt++)
        {
            var dispatched = await services.GetRequiredService<IOutboxDispatchService>().DispatchOnceAsync(
                services.GetServices<IChannelSender>().ToArray(), cancellationToken);
            foreach (var path in Directory.Exists(directory) ? Directory.GetFiles(directory, "*.eml") : [])
            {
                string code;
                await using (var stream = File.OpenRead(path))
                {
                    using var message = await MimeMessage.LoadAsync(stream, cancellationToken);
                    if (!message.To.Mailboxes.Any(mailbox =>
                        string.Equals(mailbox.Address, email, StringComparison.OrdinalIgnoreCase))) continue;
                    var match = Regex.Match(message.TextBody ?? string.Empty, @"(?<!\d)\d{6}(?!\d)");
                    if (!match.Success) continue;
                    code = match.Value;
                }
                File.Delete(path);
                return code;
            }
            if (dispatched.IsFailure || dispatched.Value == 0) await Task.Delay(100, cancellationToken);
        }
        throw new Xunit.Sdk.XunitException("No se encontró el correo pickup del destinatario.");
    }
}
