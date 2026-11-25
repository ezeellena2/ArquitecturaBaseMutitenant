using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>
/// La última falla del proveedor con la clave de la plataforma (0064 §7, B13): un control técnico que anota el worker y limpia
/// quien guarda la configuración. Va aparte de <see cref="AssistantPlatformSettings"/>, que es versionada: anotarla no le cambia
/// la versión a quien la está editando (concurrencia), ni deja un evento de seguridad, porque no la cambió una persona.
/// </summary>
public sealed class AssistantPlatformProviderIssue : Entity
{
    private AssistantPlatformProviderIssue() : base(AssistantPlatformSettings.SingletonId) { }
    public string? Code { get; private set; }
    public DateTime? AtUtc { get; private set; }

    public static AssistantPlatformProviderIssue Create() => new();

    /// <summary>Guarda la falla, o la limpia con <c>null</c>; dice si cambió algo, para no escribir la fila de más.</summary>
    public bool Record(string? code, DateTime atUtc)
    {
        if (atUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC is required.", nameof(atUtc));
        if (Code == code) return false;
        Code = code;
        AtUtc = code is null ? null : atUtc;
        return true;
    }
}
