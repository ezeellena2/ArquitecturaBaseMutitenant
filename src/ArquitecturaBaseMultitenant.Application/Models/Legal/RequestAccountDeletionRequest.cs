namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>Lleva la razón de baja y su prueba de reautenticación sin exponerlas en la representación textual del pedido.</summary>
public sealed record RequestAccountDeletionRequest(string? Reason, string? ReauthTicket)
{
    public override string ToString() => nameof(RequestAccountDeletionRequest);
}
public sealed record AccountDeletionResponse(DateTime ScheduledForUtc);
