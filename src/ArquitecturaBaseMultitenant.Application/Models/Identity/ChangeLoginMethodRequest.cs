namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record ChangeLoginMethodRequest(Guid MethodId, string? ReauthTicket)
{
    public override string ToString() => nameof(ChangeLoginMethodRequest);
}
