using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Escribe cada correo como archivo .eml para inspección y pruebas locales. No contacta un servidor SMTP.</summary>
internal sealed partial class PickupDirectoryEmailTransport(
    IOptions<EmailOptions> emailOptions,
    IOptions<SmtpOptions> smtpOptions,
    IHostEnvironment environment,
    ILogger<PickupDirectoryEmailTransport> logger) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(environment.ContentRootPath, emailOptions.Value.PickupDirectory);
        Directory.CreateDirectory(directory);
        var name = $"{Guid.NewGuid():N}.eml";
        var path = Path.Combine(directory, name);
        using var mime = MimeMessageFactory.Create(message, smtpOptions.Value);
        await mime.WriteToAsync(path, cancellationToken);
        LogSaved(logger, path);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Email saved to {Path}")]
    private static partial void LogSaved(ILogger logger, string path);
}
