namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Configuration;

/// <summary>Las opciones técnicas de Formularios (<c>Forms:*</c>). Son de capacidad, no reglas de negocio: no van en pantalla (270).</summary>
public sealed class FormsOptions
{
    public const string SectionName = "Forms";
    public FormCatalogFileOptions CatalogFiles { get; set; } = new();

    /// <summary>Al menos una operación por organización, y la instalación con lugar para una organización entera.</summary>
    public static bool IsValid(FormsOptions options) =>
        options?.CatalogFiles is { MaxPerOrganization: >= 1 and <= 1000 } files
        && files.MaxPerInstallation >= files.MaxPerOrganization && files.MaxPerInstallation <= 10_000;
}

/// <summary>
/// Cuántas operaciones de archivo de los catálogos (la vista previa, aplicar y exportar) corren a la vez entre todas las copias
/// (C11): por organización, así una no frena a las demás, y en toda la instalación. Valores de piloto.
/// </summary>
public sealed class FormCatalogFileOptions
{
    public int MaxPerOrganization { get; set; } = 2;
    public int MaxPerInstallation { get; set; } = 4;
}
