using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Contiene el host, las credenciales y el remitente del transporte SMTP. Sus valores sensibles provienen de configuración externa, no del repositorio.</summary>
internal sealed class SmtpOptions
{
    public const string SectionName = "Email:Smtp";

    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    public SecureSocketOptions Security { get; init; } = SecureSocketOptions.StartTls;

    [Required]
    public string UserName { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string FromName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string FromAddress { get; init; } = string.Empty;
}
