namespace ArquitecturaBaseMultitenant.Application.Models.StructuredData;

/// <summary>
/// Qué contactos pide una lista que arma otra área (C12 de la E12): condiciones por etiqueta unidas con «y», que comparan el texto
/// sin distinguir mayúsculas, con los valores como parámetros. <see cref="Limit"/> es el tope: se lee una fila más para saber si
/// lo pasa, y solo entonces se cuenta el total.
/// </summary>
public sealed record StructuredContactQuery(int Limit)
{
    public IReadOnlyList<StructuredContactTagCondition> Tags { get; init; } = [];
    /// <summary>El contacto que no entra, el de quien recibe («Sin incluir a quien lo recibe»).</summary>
    public Guid? ExcludedContactId { get; init; }
    /// <summary>Por nombre; con <c>true</c>, de la Z a la A.</summary>
    public bool Descending { get; init; }
}

/// <summary>Tiene alguna de esas etiquetas (<see cref="Include"/>) o ninguna.</summary>
public sealed record StructuredContactTagCondition(IReadOnlyList<string> Tags, bool Include);
