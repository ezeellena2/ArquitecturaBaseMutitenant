namespace ArquitecturaBaseMultitenant.Application.Common.Exceptions;

/// <summary>Otro pedido guardó primero una fila con la misma clave única.</summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException()
    {
    }

    public UniqueConstraintViolationException(string message)
        : base(message)
    {
    }

    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Nombre del índice único, sin los valores repetidos.</summary>
    public string? ConstraintName { get; init; }
}
