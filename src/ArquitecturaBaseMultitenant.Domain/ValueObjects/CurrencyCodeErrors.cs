using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.ValueObjects;

/// <summary>Errores de la forma de un código de moneda; la vigencia se valida en Application.</summary>
public static class CurrencyCodeErrors
{
    public const string InvalidCode = "ReferenceData.Currency.Invalid";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The currency code must have three letters.");
}
