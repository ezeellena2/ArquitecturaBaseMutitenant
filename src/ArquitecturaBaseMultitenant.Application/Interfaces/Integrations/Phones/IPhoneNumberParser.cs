using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;

/// <summary>Interpreta lo que escribe una persona y lo devuelve en E.164; las reglas de numeración son de libphonenumber.</summary>
public interface IPhoneNumberParser
{
    /// <summary>
    /// <paramref name="country"/> es ISO 3166-1 alfa-2; si el número empieza con «+», se ignora. Devuelve
    /// <c>Users.Phone.Invalid</c> o, con <see cref="PhoneUsage.Mobile"/>, <c>Users.Phone.NotMobile</c>.
    /// </summary>
    Result<PhoneNumber> Parse(string? country, string? number, PhoneUsage usage);
}
