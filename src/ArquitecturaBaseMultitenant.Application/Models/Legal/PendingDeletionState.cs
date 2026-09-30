namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>Expone la fecha programada y el comprobante temporal para cancelar una baja sin iniciar sesión.</summary>
public sealed record PendingDeletionState(DateTime ScheduledForUtc, string CancelTicket, string TimeZoneId,
    string ReturnUrl, DateTime ExpiresAtUtc)
{
    public override string ToString() => nameof(PendingDeletionState);
}
