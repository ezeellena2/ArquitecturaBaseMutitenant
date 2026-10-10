namespace ArquitecturaBaseMultitenant.Domain.Results;

/// <summary>Error de validación con los mensajes agrupados por campo (campo → mensajes).</summary>
public sealed record ValidationError : Error
{
    public const string ErrorCode = "Validation.Failed";

    public ValidationError(IReadOnlyDictionary<string, string[]> errors)
        : this(ErrorCode, "One or more validation errors occurred.", errors)
    {
    }

    /// <summary>
    /// Un error de una regla de negocio con su propio código que además se ata a un campo del formulario: por ejemplo,
    /// <c>Users.Invitation.ConsentRequired</c> en la casilla del consentimiento. El front lo reconoce por el código y lo
    /// muestra debajo del campo, igual que un error de validación.
    /// </summary>
    public ValidationError(string code, string description, IReadOnlyDictionary<string, string[]> errors)
        : this(code, description, errors, ErrorType.Validation)
    {
    }

    /// <summary>Conserva errores por campo en reglas con una clasificación propia (por ejemplo 403 o 413).</summary>
    public ValidationError(string code, string description, IReadOnlyDictionary<string, string[]> errors, ErrorType type)
        : base(code, description, type)
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
