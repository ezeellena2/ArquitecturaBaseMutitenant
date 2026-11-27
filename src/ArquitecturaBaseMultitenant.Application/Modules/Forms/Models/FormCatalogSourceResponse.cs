namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Models;

/// <summary>La definición de la fuente «Miembros» (§4): su versión y el catálogo cerrado de columnas y operadores.</summary>
public sealed record FormCatalogSourceResponse(int Version, IReadOnlyList<FormCatalogSourceColumn> Columns);

public sealed record FormCatalogSourceColumn(string Key, FormCatalogColumnType Type, IReadOnlyList<string> Operators, bool CanDetail, bool CanSort);

/// <summary>Una persona de ejemplo, sin correo, con los nombres de sus empresas y sus roles.</summary>
public sealed record FormCatalogSampleMemberRow(Guid MemberId, string Name, IReadOnlyList<string> Companies, IReadOnlyList<string> Roles);

/// <summary>Un cliente de ejemplo de Clientes, sin teléfono (C12).</summary>
public sealed record FormCatalogSampleContactRow(Guid ContactId, string Name);

/// <summary>Una etiqueta que usan los contactos de la organización, con cuántos la tienen (C12).</summary>
public sealed record FormCatalogTagRow(string Name, int Contacts);
