using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

/// <summary>Indica si los mensajes salen por SMTP o se escriben como archivos locales.</summary>
internal enum EmailDelivery
{
    Smtp,
    PickupDirectory,
}

/// <summary>Agrupa el modo de entrega y la presentación común de los correos. La selección entre SMTP y pickup se hace al registrar el transporte.</summary>
internal sealed class EmailOptions
{
    public const string SectionName = "Email";

    public EmailDelivery Delivery { get; set; } = EmailDelivery.Smtp;

    [Required]
    public string PickupDirectory { get; init; } = ".emails";

    [Required]
    public string AppName { get; init; } = "Arquitectura Base";

    [Url]
    public string? LogoUrl { get; init; }
}
