using ArquitecturaBaseMultitenant.Application.Models.Auth;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Auth;

/// <summary>Segundo factor de un operador (E9): con qué prueba y el código; nunca se registra.</summary>
public sealed record VerifyPlatformFactorHttpRequest(TwoFactorMethod? Method, string? Code)
{
    public VerifyPlatformFactorRequest ToModel() => new(Method, Code);
    public override string ToString() => nameof(VerifyPlatformFactorHttpRequest);
}

/// <summary>Confirma que el operador guardó sus ocho códigos de recuperación; completa el alta y el ingreso.</summary>
public sealed record CompletePlatformFactorHttpRequest(bool? SavedRecoveryCodes)
{
    public CompletePlatformFactorRequest ToModel() => new(SavedRecoveryCodes);
    public override string ToString() => nameof(CompletePlatformFactorHttpRequest);
}
