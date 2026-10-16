using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>
/// Qué sigue después de probar el código: abrir el authorize original o, para un operador de Plataforma (E9), el
/// segundo factor, sin sesión todavía.
/// </summary>
public enum LoginCodeNextStep
{
    [JsonStringEnumMemberName("authorize")] Authorize,
    [JsonStringEnumMemberName("platformFactor")] PlatformFactor,
}

/// <summary>
/// Authorize original que el SPA vuelve a abrir después de crear la sesión. Con <see cref="LoginCodeNextStep.PlatformFactor"/>
/// el SPA consulta el desafío pendiente en lugar de abrirlo.
/// </summary>
public sealed record VerifyLoginCodeResponse(string ReturnUrl, LoginCodeNextStep Next);
