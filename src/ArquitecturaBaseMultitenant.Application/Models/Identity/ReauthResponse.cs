namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record ReauthResponse(string ReauthTicket)
{
    public override string ToString() => nameof(ReauthResponse);
}
