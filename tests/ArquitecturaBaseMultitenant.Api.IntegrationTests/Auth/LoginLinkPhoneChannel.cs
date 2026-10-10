using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Auth;

/// <summary>Canal sin transporte, sólo para reglas neutrales de enlaces; Host(false) conserva la instalación normal.</summary>
internal sealed class LoginLinkPhoneChannel : ILoginCodeChannel
{
    public string Key => "login-link-control";
    public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
    public bool IsAvailable => true;
    public IReadOnlyList<string> Countries => [];
    public string? ContactPhone => null;
    public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => Result.Success();
    public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        throw new InvalidOperationException("Login link tests must not deliver codes.");
    public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) =>
        throw new InvalidOperationException("Login link tests must not deliver codes.");
    public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        throw new InvalidOperationException("Login link tests must not deliver codes.");
    public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) =>
        throw new InvalidOperationException("Login link tests must not deliver codes.");
}
