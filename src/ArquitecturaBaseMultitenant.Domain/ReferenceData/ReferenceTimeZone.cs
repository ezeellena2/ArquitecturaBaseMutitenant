namespace ArquitecturaBaseMultitenant.Domain.ReferenceData;

/// <summary>Representa una zona IANA seleccionable; los nombres visibles viven en sus traducciones.</summary>
public sealed class ReferenceTimeZone
{
    private ReferenceTimeZone() { }

    public string Id { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    public int? SortOrder { get; private set; }

    public static ReferenceTimeZone Create(string id, bool isEnabled, int? sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return new ReferenceTimeZone { Id = id, IsEnabled = isEnabled, SortOrder = sortOrder };
    }
}
