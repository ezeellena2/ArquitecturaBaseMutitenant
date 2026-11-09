namespace ArquitecturaBaseMultitenant.Domain.Invitations;

/// <summary>
/// «La persona aceptó recibir mensajes por WhatsApp» (decisión 179): quién lo confirmó al cargar su teléfono, y cuándo. Lo
/// guarda la invitación que sale de «Cargar teléfonos»; las de correo y las anteriores no lo tienen.
/// </summary>
public sealed record InvitationConsent
{
    public InvitationConsent(Guid confirmedByUserId, DateTime confirmedAtUtc)
    {
        if (confirmedByUserId == Guid.Empty)
            throw new ArgumentException("Who confirmed the consent is required.", nameof(confirmedByUserId));
        if (confirmedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("The consent instant must be UTC.", nameof(confirmedAtUtc));
        ConfirmedByUserId = confirmedByUserId;
        ConfirmedAtUtc = confirmedAtUtc;
    }

    public Guid ConfirmedByUserId { get; }

    public DateTime ConfirmedAtUtc { get; }
}
