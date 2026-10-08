namespace ArquitecturaBaseMultitenant.Api.Features;

/// <summary>Capacidad técnica global instalada; no representa una feature por organización.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class InstalledModuleRouteAttribute(string key) : Attribute
{
    public string Key { get; } = !string.IsNullOrWhiteSpace(key) ? key
        : throw new ArgumentException("An installed route key is required.", nameof(key));
}
