namespace ArquitecturaBaseMultitenant.Application.Models.Roles;

/// <summary>
/// Los permisos de una persona en la organización Business activa, leídos juntos: los de la organización, con los que se
/// proyectan desde una empresa, y los de cada empresa donde tiene alguno, con los de la organización incluidos.
/// </summary>
public sealed record BusinessPermissions(IReadOnlyList<string> Organization,
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> ByCompany);
