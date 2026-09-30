namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Entrega la URL del desafío Google al controller sin exponerla en la representación textual del objeto.</summary>
public sealed record GoogleChallengeResponse(string RedirectUrl)
{
    public override string ToString() => nameof(GoogleChallengeResponse);
}
