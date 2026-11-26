namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

// Los tres orígenes que administra Plataforma, separados de TenantFeatureSource del dominio para que los que suma la
// etapa del cobro no cambien el contrato (ADR 0069). Se publica con el nombre de esquema TenantFeatureSource y la
// misma descripción (OpenApiExtensions), y cada modelo lo traduce al del dominio.

/// <summary>Origen de la habilitación de un módulo por Plataforma.</summary>
public enum PlatformFeatureSource
{
    Plan,
    Manual,
    Trial,
}
