namespace ArquitecturaBaseMultitenant.Application.Models.StructuredData;

/// <summary>
/// Un contacto de la organización que da un canal de mensajería (C12 de la E12): su id, su nombre y sus etiquetas, en orden
/// alfabético. Nunca su teléfono. Un contacto sin nombre no se ofrece.
/// </summary>
public sealed record StructuredContact(Guid Id, string Name, IReadOnlyList<string> Tags);

/// <summary>
/// Los contactos hasta el tope: <see cref="Total"/> es la cantidad exacta y <see cref="ExceedsLimit"/> dice que había más que el
/// tope, que no vienen.
/// </summary>
public sealed record StructuredContactPage(IReadOnlyList<StructuredContact> Contacts, int Total, bool ExceedsLimit);

/// <summary>Una etiqueta que usan los contactos de la organización, con cuántos la tienen.</summary>
public sealed record StructuredContactTag(string Name, int Contacts);
