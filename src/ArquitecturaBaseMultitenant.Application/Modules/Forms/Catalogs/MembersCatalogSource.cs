using ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Catalogs;

/// <summary>
/// La fuente «Miembros» (§4 y C2 de la E12), definida en el código y no como vista SQL: su versión y el catálogo cerrado de
/// columnas y operadores. La lectura es del núcleo (<c>IMemberDirectoryReader</c>). Si cambian las columnas o los operadores,
/// sube <see cref="Version"/> y los catálogos guardados con la anterior quedan «No se puede usar» (<c>SourceOutdated</c>), sin
/// migrar datos.
/// </summary>
public static class MembersCatalogSource
{
    public const int Version = 1;

    /// <summary>El nombre visible de la cuenta: siempre es el texto de la opción.</summary>
    public const string Name = "name";
    /// <summary>Los roles de la organización: los valores son ids de roles que existen.</summary>
    public const string Role = "role";
    /// <summary>Las empresas con membresía activa: los valores son ids de empresas que existen.</summary>
    public const string Company = "company";
    /// <summary>Si el miembro está activo: un solo valor, <c>true</c> o <c>false</c>.</summary>
    public const string Active = "active";

    public static FormCatalogSourceResponse Definition { get; } = new(Version,
    [
        new(Name, FormCatalogColumnType.Text, [FormCatalogOperators.Contains, FormCatalogOperators.EqualTo], CanDetail: false, CanSort: true),
        new(Role, FormCatalogColumnType.List, [FormCatalogOperators.HasAny, FormCatalogOperators.HasNone], CanDetail: true, CanSort: false),
        new(Company, FormCatalogColumnType.List, [FormCatalogOperators.HasAny, FormCatalogOperators.HasNone], CanDetail: true, CanSort: false),
        new(Active, FormCatalogColumnType.Boolean, [FormCatalogOperators.Is], CanDetail: false, CanSort: false),
    ]);
}

/// <summary>
/// La fuente «Contactos» (§4b y C12 de la E12): los contactos de Clientes que da el canal por un puerto neutral, filtrados por
/// sus etiquetas, con la misma gramática que «Miembros».
/// </summary>
public static class ContactsCatalogSource
{
    public const int Version = 1;

    /// <summary>El nombre del contacto: siempre es el texto de la opción.</summary>
    public const string Name = "name";
    /// <summary>Las etiquetas del contacto, como textos que se comparan sin distinguir mayúsculas.</summary>
    public const string Tags = "tags";

    public static FormCatalogSourceResponse Definition { get; } = new(Version,
    [
        new(Name, FormCatalogColumnType.Text, [], CanDetail: false, CanSort: true),
        new(Tags, FormCatalogColumnType.List, [FormCatalogOperators.HasAny, FormCatalogOperators.HasNone], CanDetail: true, CanSort: false),
    ]);
}

/// <summary>Los operadores de las condiciones de una fuente (C5), tal como viajan en los filtros.</summary>
public static class FormCatalogOperators
{
    public const string Contains = "contains";
    public const string EqualTo = "equals";
    public const string HasAny = "hasAny";
    public const string HasNone = "hasNone";
    public const string Is = "is";
}
