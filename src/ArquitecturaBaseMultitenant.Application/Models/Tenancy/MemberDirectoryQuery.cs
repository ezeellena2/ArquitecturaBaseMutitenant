namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

/// <summary>
/// Qué miembros pide una lista que arma otra área (C2 de la E12), con condiciones de una lista cerrada unidas con «y»: los
/// valores viajan como parámetros y nunca como SQL (ADR 0053). <see cref="Limit"/> es el tope de la lista: el lector trae una
/// fila más para saber si lo pasa, y solo entonces cuenta el total.
/// </summary>
public sealed record MemberDirectoryQuery(int Limit)
{
    /// <summary>El nombre visible de la cuenta, sin distinguir mayúsculas ni tildes.</summary>
    public IReadOnlyList<MemberDirectoryNameCondition> Names { get; init; } = [];
    /// <summary>Roles de la organización: cualquier asignación, de organización o de empresa.</summary>
    public IReadOnlyList<MemberDirectoryIdCondition> Roles { get; init; } = [];
    /// <summary>Empresas con una membresía activa.</summary>
    public IReadOnlyList<MemberDirectoryIdCondition> Companies { get; init; } = [];
    /// <summary>Si el miembro está activo; cada valor es una condición más.</summary>
    public IReadOnlyList<bool> Active { get; init; } = [];
    /// <summary>El miembro que no entra, el de quien recibe («Sin incluir a quien lo recibe»).</summary>
    public Guid? ExcludedMemberId { get; init; }
    /// <summary>Qué nombres trae cada fila además del suyo: los de sus roles o los de sus empresas.</summary>
    public MemberDirectoryDetail Detail { get; init; }
    /// <summary>Por nombre; con <c>true</c>, de la Z a la A.</summary>
    public bool Descending { get; init; }
}

/// <summary>El nombre contiene el texto o es igual a él (<see cref="Exact"/>).</summary>
public sealed record MemberDirectoryNameCondition(string Value, bool Exact);

/// <summary>Tiene alguno de esos ids (<see cref="Include"/>) o ninguno.</summary>
public sealed record MemberDirectoryIdCondition(IReadOnlyList<Guid> Ids, bool Include);

/// <summary>Los nombres que acompañan a cada miembro de la lista.</summary>
public enum MemberDirectoryDetail
{
    None,
    Roles,
    Companies,
}
