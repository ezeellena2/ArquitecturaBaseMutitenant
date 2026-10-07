namespace ArquitecturaBaseMultitenant.Application.Models.Notifications;

/// <summary>Catálogo cerrado de avisos de una cuenta; cada variante recibe sólo los datos necesarios.</summary>
public abstract record AccountNotice
{
    private AccountNotice() { }

    public string? RecipientName { get; init; }

    public sealed record LoginMethodChanged(string Change,
        ArquitecturaBaseMultitenant.Domain.Authentication.LoginMethodType MethodType,
        string MaskedMethod, DateTime OccurredAtUtc, string TimeZoneId, string ActionUrl)
        : AccountNotice;

    public sealed record ReviewLoginMethods(string OrganizationName, string ActionUrl) : AccountNotice;

    public sealed record OrganizationRejected(string OrganizationName) : AccountNotice;
    public sealed record OrganizationApproved(string OrganizationName) : AccountNotice;

    public sealed record PublicPageModerated(string OrganizationName, bool IsBlocked) : AccountNotice;

    /// <summary>
    /// Baja programada. Si la inició Plataforma, el texto lo dice; una cuenta suspendida no puede cancelarla y el aviso
    /// ofrece el correo de soporte, si está cargado.
    /// </summary>
    public sealed record DeletionRequested(DateTime RequestedAtUtc, DateTime ScheduledForUtc,
        string TimeZoneId, string ActionUrl) : AccountNotice
    {
        public bool RequestedByPlatform { get; init; }
        public bool CanCancel { get; init; } = true;
        public ArquitecturaBaseMultitenant.Domain.ValueObjects.Email? SupportEmail { get; init; }
    }

    public sealed record DeletionCancelled(DateTime OccurredAtUtc, string TimeZoneId,
        string ActionUrl) : AccountNotice;

    public sealed record AccountDeleted : AccountNotice;

    /// <summary>Al método nuevo de «Recuperar mi cuenta»: no revela si había una cuenta.</summary>
    public sealed record RecoveryReceived : AccountNotice;

    /// <summary>
    /// Al correo del operador invitado (lienzo v90, Mensajes › «Invitación como operador»): quién invita, las claves
    /// de los permisos, que se muestran por nombre, el vencimiento en la zona dada y el enlace para entrar a
    /// Plataforma. Sale solo por correo.
    /// </summary>
    public sealed record OperatorInvited(string? InviterName, IReadOnlyList<string> Permissions, DateTime ExpiresAtUtc,
        string TimeZoneId, string ActionUrl) : AccountNotice;

    /// <summary>Al correo nuevo, que ya es el principal de la cuenta, con el enlace para ingresar.</summary>
    public sealed record RecoveryApproved(string ActionUrl) : AccountNotice;

    /// <summary>
    /// A los métodos que siguen andando de la cuenta encontrada, cuando alguien registra un pedido para recuperarla.
    /// Nunca a quien hizo el pedido ni con el método nuevo, ni siquiera tapado (ADR 0041). Sin botón; la nota lleva
    /// el correo de soporte de la configuración de la plataforma.
    /// </summary>
    public sealed record RecoveryRequested(DateTime RequestedAtUtc, string TimeZoneId) : AccountNotice
    {
        public ArquitecturaBaseMultitenant.Domain.ValueObjects.Email? SupportEmail { get; init; }
    }

    public sealed record RecoveryRejected : AccountNotice;
}
