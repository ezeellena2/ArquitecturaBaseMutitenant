namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

public sealed record GoogleChallengeResponse(string RedirectUrl)
{
    public override string ToString() => nameof(GoogleChallengeResponse);
}
