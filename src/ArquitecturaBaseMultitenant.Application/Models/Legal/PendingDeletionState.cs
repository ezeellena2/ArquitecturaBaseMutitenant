namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

public sealed record PendingDeletionState(DateTime ScheduledForUtc, string CancelTicket, string TimeZoneId,
    string ReturnUrl, DateTime ExpiresAtUtc)
{
    public override string ToString() => nameof(PendingDeletionState);
}
