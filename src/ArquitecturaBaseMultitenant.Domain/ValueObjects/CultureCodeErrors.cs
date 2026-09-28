using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.ValueObjects;

/// <summary>Errores de la forma de una cultura; la habilitación se valida en Application.</summary>
public static class CultureCodeErrors
{
    public const string InvalidCode = "ReferenceData.Culture.Invalid";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The culture code must have a BCP 47 language-region shape.");
}
