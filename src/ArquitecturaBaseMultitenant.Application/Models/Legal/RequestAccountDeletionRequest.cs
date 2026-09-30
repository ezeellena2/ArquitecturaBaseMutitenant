namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

public sealed record RequestAccountDeletionRequest(string? Reason, string? ReauthTicket)
{
    public override string ToString() => nameof(RequestAccountDeletionRequest);
}
public sealed record AccountDeletionResponse(DateTime ScheduledForUtc);
