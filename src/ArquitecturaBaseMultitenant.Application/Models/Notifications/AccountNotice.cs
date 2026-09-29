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

    public sealed record ReviewLoginMethods(string OrganizationName) : AccountNotice;

    public sealed record DeletionRequested(DateTime RequestedAtUtc, DateTime ScheduledForUtc,
        string TimeZoneId, string ActionUrl) : AccountNotice;

    public sealed record DeletionCancelled(DateTime OccurredAtUtc, string TimeZoneId,
        string ActionUrl) : AccountNotice;

    public sealed record AccountDeleted : AccountNotice;

    public sealed record RecoveryReceived : AccountNotice;

    public sealed record RecoveryApproved : AccountNotice;

    public sealed record RecoveryRejected : AccountNotice;
}
