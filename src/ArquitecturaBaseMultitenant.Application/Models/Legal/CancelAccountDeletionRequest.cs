namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

public sealed record CancelAccountDeletionRequest(string? CancelTicket)
{
    public override string ToString() => nameof(CancelAccountDeletionRequest);
}

public sealed record CancelAccountDeletionResponse(Guid UserId, string ReturnUrl);
