using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

internal enum EmailDelivery
{
    Smtp,
    PickupDirectory,
}

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
