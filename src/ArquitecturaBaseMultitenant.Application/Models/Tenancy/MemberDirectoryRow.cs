namespace ArquitecturaBaseMultitenant.Application.Models.Tenancy;

/// <summary>
/// Un miembro de la lista: su id, el nombre visible de la cuenta y, si se pidieron, los nombres de sus roles o de sus empresas
/// (<see cref="Details"/>), en orden alfabético. Nunca un correo, un teléfono ni un método de ingreso.
/// </summary>
public sealed record MemberDirectoryRow(Guid MemberId, string Name, IReadOnlyList<string> Details);

/// <summary>
/// La lista hasta su tope: <see cref="Total"/> es la cantidad exacta y <see cref="ExceedsLimit"/> dice que había más filas que el
/// tope, que no vienen.
/// </summary>
public sealed record MemberDirectoryResponse(IReadOnlyList<MemberDirectoryRow> Rows, int Total, bool ExceedsLimit);

/// <summary>Una persona que se puede elegir de ejemplo, con los nombres de sus empresas y de sus roles.</summary>
public sealed record MemberDirectorySampleRow(Guid MemberId, string Name, IReadOnlyList<string> Companies, IReadOnlyList<string> Roles);

/// <summary>Un rol o una empresa de la organización, por su id y su nombre.</summary>
public sealed record MemberDirectoryNameRow(Guid Id, string Name);
