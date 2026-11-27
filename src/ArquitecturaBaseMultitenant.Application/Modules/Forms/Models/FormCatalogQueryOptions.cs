namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>El filtro de origen del listado de catálogos.</summary>
public enum FormCatalogSourceFilter
{
    All,
    Manual,
    Members,
    Contacts,
}

/// <summary>El filtro de estado del listado de catálogos y del de opciones.</summary>
public enum FormCatalogStatusFilter
{
    All,
    Active,
    Inactive,
}

/// <summary>Pegar agrega e importar un archivo reemplaza (C7).</summary>
public enum FormCatalogImportMode
{
    Append,
    Replace,
}

/// <summary>Subir o bajar una opción: intercambia su posición con la vecina (C4).</summary>
public enum FormCatalogMoveDirection
{
    Up,
    Down,
}

/// <summary>La columna por la que «Ordenar» reordena todas las opciones al guardar (224).</summary>
public enum FormCatalogItemSortColumn
{
    Text,
    Code,
}

/// <summary>El tipo de una columna de la fuente: texto, lista o sí/no (§4).</summary>
public enum FormCatalogColumnType
{
    Text,
    List,
    Boolean,
}
