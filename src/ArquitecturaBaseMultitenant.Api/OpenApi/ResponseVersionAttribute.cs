

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>Ruta de propiedades CLR hasta la versión uint del éxito compuesto declarado.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ResponseVersionAttribute(string propertyPath) : Attribute
{
    public string PropertyPath { get; } = propertyPath;
}
