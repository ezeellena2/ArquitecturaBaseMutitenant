using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Domain.Common;

/// <summary>Errores comunes de las transiciones de datos compartidos.</summary>
public static class PartyErrors
{
    public const string NotAllowedCode = "Tenancy.Party.NotAllowed";
    public static readonly Error NotAllowed = Error.Forbidden(NotAllowedCode,
        "The active party cannot perform this transition.");
}
