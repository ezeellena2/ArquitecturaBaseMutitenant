namespace ArquitecturaBaseMultitenant.Application.Models.Notifications;

/// <summary>Catálogo cerrado de avisos de una cuenta; cada variante recibe sólo los datos necesarios.</summary>
public abstract record AccountNotice
{
    private AccountNotice() { }

    public sealed record LoginMethodChanged(string Change, string MaskedMethod, DateTime OccurredAtUtc)
        : AccountNotice;

    public sealed record ReviewLoginMethods(string OrganizationName) : AccountNotice;

    public sealed record DeletionRequested(DateTime ScheduledForUtc) : AccountNotice;

    public sealed record DeletionCancelled : AccountNotice;

    public sealed record AccountDeleted : AccountNotice;

    public sealed record RecoveryReceived : AccountNotice;

    public sealed record RecoveryApproved : AccountNotice;

    public sealed record RecoveryRejected : AccountNotice;
}
