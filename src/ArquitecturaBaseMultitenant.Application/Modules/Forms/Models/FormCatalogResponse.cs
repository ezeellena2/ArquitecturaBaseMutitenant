using ArquitecturaBaseMultitenant.Domain.Forms;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>
/// El detalle de un catálogo. <see cref="Roles"/> y <see cref="Companies"/> traen el nombre de cada id que usa el filtro, así
/// quien solo lo ve lo lee sin pedir esas listas; un id que ya no existe no viene. <see cref="Sample"/> es la persona o el
/// cliente de ejemplo guardado en los filtros.
/// </summary>
public sealed record FormCatalogResponse(Guid Id, string Name, string? Description, FormCatalogSource Source, FormCatalogValueType ValueType,
    FormCatalogFilters? Filters, IReadOnlyList<FormCatalogReference>? Roles, IReadOnlyList<FormCatalogReference>? Companies,
    FormCatalogSample? Sample, bool IsActive, bool Usable, FormCatalogUnusableReason? UnusableReason, bool CanManage,
    FormCatalogItemCounts ItemCounts, DateTime ModifiedAtUtc, string? ModifiedBy, uint Version);

/// <summary>Los filtros de un catálogo de miembros o de contactos (C5): condiciones unidas con «y».</summary>
public sealed record FormCatalogFilters(int SourceVersion, IReadOnlyList<FormCatalogCondition> Conditions, bool ExcludeRecipient,
    string? Detail, FormCatalogFilterSort? Sort);

/// <summary>Una condición <c>{column, operator, values[]}</c>, con columna y operador de la fuente (§4 y §4b).</summary>
public sealed record FormCatalogCondition(string Column, string Operator, IReadOnlyList<string> Values);

public sealed record FormCatalogFilterSort(string Column, bool Descending);

/// <summary>Un id con su nombre: un rol o una empresa del filtro, o una fila de esas listas.</summary>
public sealed record FormCatalogReference(Guid Id, string Name);

public sealed record FormCatalogSample(Guid Id, string Name, string? Detail);

public sealed record FormCatalogItemCounts(int Total, int Active);

/// <summary>El id del servidor de cada opción nueva, por el id local con que la mandó la pantalla.</summary>
public sealed record FormCatalogItemIdentity(string LocalId, Guid ItemId);

/// <summary>El alta: el detalle y el mapa de ids de las opciones nuevas.</summary>
public sealed record FormCatalogCreatedResponse(FormCatalogResponse Catalog, IReadOnlyList<FormCatalogItemIdentity> ItemIdentityMap);

/// <summary>Guardar: la versión nueva del catálogo y el mapa de ids de las opciones nuevas (C3).</summary>
public sealed record FormCatalogUpdatedResponse(uint Version, IReadOnlyList<FormCatalogItemIdentity> ItemIdentityMap);
