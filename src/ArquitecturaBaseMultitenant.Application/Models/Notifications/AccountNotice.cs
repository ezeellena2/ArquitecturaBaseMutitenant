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
    /// Al dueño de un correo que alguien dio como método nuevo de «Recuperar mi cuenta», en lugar del código (ADR 0041).
    /// </summary>
    public sealed record RecoveryMethodInUse : AccountNotice;

    public sealed record RecoveryApproved : AccountNotice;

    public sealed record RecoveryRejected : AccountNotice;
}
