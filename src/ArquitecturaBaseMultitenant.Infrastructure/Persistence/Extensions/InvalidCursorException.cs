namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;

/// <summary>El valor de after no codifica una posición de paginado válida.</summary>
public sealed class InvalidCursorException : Exception
{
    public InvalidCursorException() : base("Invalid list cursor.")
    {
    }
}
