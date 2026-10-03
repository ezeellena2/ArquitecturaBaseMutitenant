using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Phones;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using PhoneNumbers;
using DomainPhoneNumber = ArquitecturaBaseMultitenant.Domain.ValueObjects.PhoneNumber;

namespace ArquitecturaBaseMultitenant.Infrastructure.Phones;

/// <summary>
/// Interpreta un número con el país elegido usando los metadatos de libphonenumber y lo devuelve en E.164.
/// Para un celular, si falta el prefijo de celular del país (el 9 de Argentina) lo completa con el que define la
/// librería: ninguna regla de país está escrita acá.
/// </summary>
public sealed class LibPhoneNumberParser : IPhoneNumberParser
{
    private static readonly PhoneNumberUtil Util = PhoneNumberUtil.GetInstance();

    public Result<DomainPhoneNumber> Parse(string? country, string? number, PhoneUsage usage)
    {
        var text = number?.Trim();
        if (string.IsNullOrEmpty(text)) return PhoneErrors.Invalid;
        var region = text.StartsWith('+') ? null : country?.Trim().ToUpperInvariant();
        if (region is not null && !Util.GetSupportedRegions().Contains(region)) return PhoneErrors.Invalid;
        if (region is null && !text.StartsWith('+')) return PhoneErrors.Invalid;

        PhoneNumber parsed;
        try
        {
            parsed = Util.Parse(text, region);
        }
        catch (NumberParseException)
        {
            return PhoneErrors.Invalid;
        }

        if (usage == PhoneUsage.Mobile && !IsMobile(parsed)) parsed = WithMobileToken(parsed) ?? parsed;
        if (!Util.IsValidNumber(parsed)) return PhoneErrors.Invalid;
        if (usage == PhoneUsage.Mobile && !IsMobile(parsed)) return PhoneErrors.NotMobile;
        return DomainPhoneNumber.Create(Util.Format(parsed, PhoneNumberFormat.E164));
    }

    public string Mask(DomainPhoneNumber phone)
    {
        ArgumentNullException.ThrowIfNull(phone);
        var groups = Util.Format(Util.Parse(phone.Value, null), PhoneNumberFormat.INTERNATIONAL)
            .Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (groups.Length < 3) return string.Concat(phone.Value.AsSpan(0, Math.Min(3, phone.Value.Length)), " •••• ", phone.Value.AsSpan(Math.Max(0, phone.Value.Length - 4)));
        groups[^2] = "••••";
        return string.Join(' ', groups);
    }

    private static bool IsMobile(PhoneNumber number) =>
        Util.GetNumberType(number) is PhoneNumberType.MOBILE or PhoneNumberType.FIXED_LINE_OR_MOBILE;

    private static PhoneNumber? WithMobileToken(PhoneNumber number)
    {
        var token = PhoneNumberUtil.GetCountryMobileToken(number.CountryCode);
        if (string.IsNullOrEmpty(token)) return null;
        var nationalNumber = Util.GetNationalSignificantNumber(number);
        if (nationalNumber.StartsWith(token, StringComparison.Ordinal)) return null;
        try
        {
            var candidate = Util.Parse($"+{number.CountryCode}{token}{nationalNumber}", null);
            return Util.IsValidNumber(candidate) && IsMobile(candidate) ? candidate : null;
        }
        catch (NumberParseException)
        {
            return null;
        }
    }
}
