using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.ValueObjects;

public static class TaxIdErrors
{
    public const string InvalidCode = "TaxIds.TaxId.Invalid";

    public static readonly Error Invalid = Error.Validation(InvalidCode, "The tax identification number is invalid.");
}
