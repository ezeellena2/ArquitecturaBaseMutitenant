namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>Lleva el comprobante temporal que cancela una baja pendiente sin incluirlo en la representación textual.</summary>
public sealed record CancelAccountDeletionRequest(string? CancelTicket)
{
    public override string ToString() => nameof(CancelAccountDeletionRequest);
}

public sealed record CancelAccountDeletionResponse(Guid UserId, string ReturnUrl);
