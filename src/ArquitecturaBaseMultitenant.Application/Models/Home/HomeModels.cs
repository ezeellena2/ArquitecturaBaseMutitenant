namespace ArquitecturaBaseMultitenant.Application.Models.Home;

/// <summary>
/// Inicio de la organización (ADR 0040). Quien tiene permisos de la organización ve las tarjetas de sus áreas; quien
/// solo tiene roles en empresas ve «Tus empresas». Debajo, «Para resolver».
/// </summary>
public sealed record HomeResponse(HomeOrganizationSummary? Organization, IReadOnlyList<HomeCompanyCard> Companies,
    IReadOnlyList<PendingItem> Pending);

/// <summary>Una tarjeta por área, con su dato real; <c>null</c> si quien mira no tiene el permiso del área.</summary>
public sealed record HomeOrganizationSummary(HomeCount? Users, HomeCount? Roles, HomeCount? Companies,
    HomePublicPage? PublicPage, HomeSettings? Settings, HomeAudit? Audit);

public sealed record HomeCount(int Total);

public enum HomePublicPageState
{
    Published,
    Draft,
    Blocked,
}

public sealed record HomePublicPage(HomePublicPageState State);

/// <summary>El dominio de correo de la organización; <c>null</c> si no tiene.</summary>
public sealed record HomeSettings(string? Domain);

/// <summary>El último cambio de la auditoría; <c>null</c> si todavía no hay.</summary>
public sealed record HomeAudit(DateTime? LastChangeAtUtc);

/// <summary>«Tus empresas»: una empresa donde la persona tiene roles; los miembros, solo si puede verlos.</summary>
public sealed record HomeCompanyCard(Guid CompanyId, string Name, IReadOnlyList<string> Roles, int? MemberCount);

/// <summary>
/// Una fila de «Para resolver». <c>Area</c> y <c>Kind</c> son claves estables: el front arma el texto, el estado y la
/// acción. <c>SinceUtc</c> ordena; <c>DueAtUtc</c> es la fecha del detalle (por ejemplo, cuándo se elimina).
/// <c>CompanyId</c> y <c>CompanyName</c> vienen cuando el pendiente es de una empresa (Inicio-Miembro).
/// </summary>
public sealed record PendingItem(string Area, string Kind, string? Subject, bool Urgent, DateTime? SinceUtc,
    DateTime? DueAtUtc, Guid? TargetId, Guid? CompanyId = null, string? CompanyName = null);

/// <summary>
/// Quién mira el inicio y sus permisos, calculados una sola vez por pedido: los de la organización y, si no tiene
/// ninguno (Inicio-Miembro), los de cada empresa donde tiene roles.
/// </summary>
public sealed record PendingItemContext(Guid UserId, IReadOnlyCollection<string> OrganizationPermissions,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>>? CompanyPermissions = null)
{
    public bool Can(string permission) => OrganizationPermissions.Contains(permission, StringComparer.Ordinal);

    /// <summary>Las empresas donde tiene el permiso, siempre en el mismo orden.</summary>
    public IReadOnlyList<Guid> CompaniesWhere(string permission) =>
    [
        .. (CompanyPermissions ?? new Dictionary<Guid, IReadOnlyList<string>>())
            .Where(company => company.Value.Contains(permission, StringComparer.Ordinal))
            .Select(company => company.Key)
            .Order(),
    ];
}
