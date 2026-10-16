using System.Text.Json.Serialization;

namespace ArquitecturaBaseMultitenant.Application.Models.Auth;

/// <summary>Con qué prueba un operador el segundo factor (E9): el código del autenticador o un código de recuperación.</summary>
public enum TwoFactorMethod
{
    [JsonStringEnumMemberName("authenticator")] Authenticator,
    [JsonStringEnumMemberName("recoveryCode")] RecoveryCode,
}

/// <summary>El paso del desafío pendiente de Plataforma: probar el factor, darlo de alta o guardar los códigos.</summary>
public enum PlatformFactorState
{
    [JsonStringEnumMemberName("verify")] Verify,
    [JsonStringEnumMemberName("setup")] Setup,
    [JsonStringEnumMemberName("recoveryCodes")] RecoveryCodes,
}

/// <summary>Qué sigue después de probar el factor: el authorize original o mostrar los códigos del alta.</summary>
public enum PlatformFactorNextStep
{
    [JsonStringEnumMemberName("authorize")] Authorize,
    [JsonStringEnumMemberName("recoveryCodes")] RecoveryCodes,
}

/// <summary>
/// El desafío pendiente ligado al navegador (E9), que dura 10 minutos sin renovarse. Según <see cref="State"/>: verify
/// trae solo el vencimiento; setup, la clave y el otpauth para el QR que arma el navegador; recoveryCodes, los ocho
/// códigos del alta hasta completarla. Nada de esto se guarda en el navegador ni se registra.
/// </summary>
public sealed record PlatformFactorChallengeResponse(
    PlatformFactorState State,
    DateTime ExpiresAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Secret = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OtpauthUri = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? RecoveryCodes = null)
{
    public const int RecoveryCodeCount = 8;

    public override string ToString() => nameof(PlatformFactorChallengeResponse);
}

/// <summary>El código del autenticador o uno de recuperación; nunca se registra.</summary>
public sealed record VerifyPlatformFactorRequest(TwoFactorMethod? Method, string? Code)
{
    public override string ToString() => nameof(VerifyPlatformFactorRequest);
}

/// <summary>
/// authorize trae el retorno validado del ingreso; recoveryCodes no trae retorno: el SPA consulta el desafío y muestra
/// los códigos del alta.
/// </summary>
public sealed record VerifyPlatformFactorResponse(
    PlatformFactorNextStep Next,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReturnUrl = null);

/// <summary>Confirma que el operador guardó los ocho códigos; sólo vale <c>true</c>.</summary>
public sealed record CompletePlatformFactorRequest(bool? SavedRecoveryCodes)
{
    public override string ToString() => nameof(CompletePlatformFactorRequest);
}

/// <summary>El retorno validado del ingreso: la sesión de Plataforma ya existe y el SPA abre el authorize.</summary>
public sealed record CompletePlatformFactorResponse(string ReturnUrl);
