using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

/// <summary>
/// Quién ve qué de una falla del proveedor según quién paga (ADR 0064 §7, B13). Con la clave de la plataforma, lo que arregla el
/// operador (la clave rechazada o ilegible, el cupo de la cuenta del proveedor y el proveedor desconocido) no se le dice a la
/// persona ni a la organización: ven «El asistente no está disponible por ahora.». Con la clave propia lo arregla el Dueño, y
/// cada código llega como es. Lo que depende del pedido (el modelo, el archivo, otro rechazo, lo incierto o sin conexión) llega
/// como es en los dos casos.
/// </summary>
public static class AssistantProviderIssuePolicy
{
    private static readonly string[] FixedByOperator =
    [
        AssistantErrors.ProviderCredentialRejectedCode, AssistantErrors.ProviderLimitedCode,
        AssistantErrors.ProviderKeyUnreadableCode, AssistantErrors.ProviderUnsupportedProviderCode,
    ];

    /// <summary>El código que ve quien hizo la consulta. Un operador de Plataforma ve el de su clave.</summary>
    public static string? Shown(string? code, AssistantKeyFunding funding, Access access) =>
        funding == AssistantKeyFunding.PlatformKey && access != Access.Platform && code is not null && FixedByOperator.Contains(code)
            ? AssistantErrors.ProviderPlatformUnavailableCode : code;

    /// <summary>Si la falla queda en la configuración de quien paga.</summary>
    public static bool IsRecorded(string code) => AssistantErrors.ProviderRecordedIssueCodes.Contains(code);
}
