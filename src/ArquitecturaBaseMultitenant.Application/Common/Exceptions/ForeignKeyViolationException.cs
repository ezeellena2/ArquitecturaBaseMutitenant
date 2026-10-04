namespace ArquitecturaBaseMultitenant.Application.Common.Exceptions;

/// <summary>
/// Otro pedido cambió primero la fila del otro lado de una clave foránea: borró la fila a la que apunta lo que se
/// guarda, o guardó una fila que apunta a la que se borra.
/// </summary>
public sealed class ForeignKeyViolationException : Exception
{
    public ForeignKeyViolationException()
    {
    }

    public ForeignKeyViolationException(string message)
        : base(message)
    {
    }

    public ForeignKeyViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Nombre de la clave foránea, sin los valores.</summary>
    public string? ConstraintName { get; init; }
}
