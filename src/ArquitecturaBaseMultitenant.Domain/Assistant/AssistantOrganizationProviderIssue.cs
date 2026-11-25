using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.Assistant;

/// <summary>
/// La última falla del proveedor con la clave propia de una organización (0064 §7, B13): privada, como su configuración, y aparte
/// de <see cref="AssistantOrganizationSettings"/>, que es versionada: anotarla no le cambia la versión al Dueño que la está editando.
/// La anota el worker y la limpia quien guarda la configuración.
/// </summary>
public sealed class AssistantOrganizationProviderIssue : Entity, ITenantOwned
{
    private AssistantOrganizationProviderIssue() { }
    public Guid TenantId { get; private set; }
    public string? Code { get; private set; }
    public DateTime? AtUtc { get; private set; }

    public static AssistantOrganizationProviderIssue Create() => new();

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
