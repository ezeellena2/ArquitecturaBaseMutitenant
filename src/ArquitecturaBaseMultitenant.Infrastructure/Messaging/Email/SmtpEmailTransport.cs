using ArquitecturaBaseMultitenant.Application.Models.Messaging;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>
/// Envía correos por SMTP mediante una conexión reutilizable durante un lote, y clasifica cada intento (N5 y N8):
/// <list type="bullet">
/// <item>hasta que el servidor acepta al destinatario no salió nada: la conexión, el TLS, el saludo, las credenciales, el
/// remitente o un 4xx al destinatario vuelven a pendiente; un 535 además deja el chequeo <c>email</c> en Degraded;</item>
/// <item>un 5xx al destinatario falla: esa dirección no lo recibe;</item>
/// <item>desde ahí (DATA y el cuerpo), una respuesta del servidor que lo rechaza dice que no lo tomó (RFC 5321): un 4xx vuelve
/// a pendiente y un 5xx falla; un corte o la espera vencida no dicen si llegó, y queda «Sin confirmar».</item>
/// </list>
/// Si falla, descarta la conexión para que el próximo intento empiece limpio.
/// </summary>
internal sealed partial class SmtpEmailTransport(IOptions<SmtpOptions> options, EmailHealth health,
    ILogger<SmtpEmailTransport> logger) : IEmailTransport, IDisposable
{
    // La espera de cada operación; la de todo el envío la pone el outbox, que la corta antes.
    private const int TimeoutMilliseconds = 30_000;
    private TrackingSmtpClient? _client;

    public async Task<EmailSendOutcome> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        MimeMessage mime;
        try { mime = MimeMessageFactory.Create(message, settings); }
        catch (ParseException) { return EmailSendOutcome.Rejected; } // Una dirección que no se puede armar no sale nunca.
        using (mime)
        {
            var client = _client ??= new TrackingSmtpClient { Timeout = TimeoutMilliseconds };
            client.BeginMessage();
            var sending = false;
            try
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(settings.Host, settings.Port, settings.Security, cancellationToken);
                    await client.AuthenticateAsync(settings.UserName, settings.Password, cancellationToken);
                }

                sending = true;
                await client.SendAsync(mime, cancellationToken);
            }
            catch (Exception exception) when (exception is not ObjectDisposedException)
            {
                // Una conexión fallida no se comparte con el siguiente mensaje del lote.
                var outcome = Classify(exception, sending, client.RecipientAccepted);
                Drop();
                if (outcome == EmailSendOutcome.CredentialsRejected) health.ReportCredentialsRejected();
                // Sin el destinatario ni el texto del servidor: solo la clase, el código SMTP y el tipo de la falla.
                LogAttemptEnded(logger, outcome, (exception as SmtpCommandException)?.StatusCode is { } status ? (int)status : null,
                    exception.GetType().Name);
                return outcome;
            }

            health.ReportSent();
            return EmailSendOutcome.Sent;
        }
    }

    private static EmailSendOutcome Classify(Exception exception, bool sending, bool recipientAccepted) => exception switch
    {
        AuthenticationException => EmailSendOutcome.CredentialsRejected,
        // Un 530 antes del cuerpo: el servidor pide las credenciales otra vez. No salió nada.
        ServiceNotAuthenticatedException => EmailSendOutcome.CredentialsRejected,
        SmtpCommandException { ErrorCode: SmtpErrorCode.RecipientNotAccepted } command =>
            IsTransient(command.StatusCode) ? EmailSendOutcome.NotSent : EmailSendOutcome.RecipientRejected,
        // El servidor contestó: no lo tomó. Antes del destinatario (el saludo, el remitente) habla del servidor y se reintenta.
        SmtpCommandException command when !recipientAccepted || IsTransient(command.StatusCode) => EmailSendOutcome.NotSent,
        SmtpCommandException => EmailSendOutcome.Rejected,
        // Al mandar, el destinatario pide SMTPUTF8 y el servidor no lo tiene: volver a probar daría lo mismo. Al conectar o al
        // autenticarse (un servidor sin STARTTLS o sin AUTH) no salió nada, y cae abajo: se reintenta, como en ArquitecturaBase.
        NotSupportedException when sending => EmailSendOutcome.Rejected,
        // La red, el TLS, el protocolo o la espera: sin destinatario aceptado no salió nada; con él, pudo haber llegado.
        _ => recipientAccepted ? EmailSendOutcome.Uncertain : EmailSendOutcome.NotSent,
    };

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SMTP attempt ended as {Outcome} (SMTP status {StatusCode}, {ExceptionType})")]
    private static partial void LogAttemptEnded(ILogger logger, EmailSendOutcome outcome, int? statusCode, string exceptionType);

    private static bool IsTransient(SmtpStatusCode statusCode) => (int)statusCode is >= 400 and < 500;

    private void Drop()
    {
        _client?.Dispose();
        _client = null;
    }

    public void Dispose() => Drop();

    /// <summary>
    /// Anota si el servidor ya aceptó al destinatario: desde ahí MailKit sigue con DATA y el cuerpo, y un corte ya no dice si
    /// llegó. Antes, ni el cuerpo salió.
    /// </summary>
    private sealed class TrackingSmtpClient : SmtpClient
    {
        public bool RecipientAccepted { get; private set; }

        public void BeginMessage() => RecipientAccepted = false;

        protected override void OnRecipientAccepted(MimeMessage message, MailboxAddress mailbox, SmtpResponse response)
        {
            RecipientAccepted = true;
            base.OnRecipientAccepted(message, mailbox, response);
        }
    }
}
