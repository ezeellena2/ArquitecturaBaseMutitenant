namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>Lleva la razón de baja sin exponerla en la representación textual del pedido.</summary>
public sealed record RequestAccountDeletionRequest(string? Reason)
{
    public override string ToString() => nameof(RequestAccountDeletionRequest);
}
public sealed record AccountDeletionResponse(DateTime ScheduledForUtc);
