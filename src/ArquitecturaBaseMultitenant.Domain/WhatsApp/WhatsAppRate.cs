using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Las categorías con que Meta cobra un mensaje: las tres de plantillas y la de servicio.</summary>
public enum WhatsAppPricingCategory { Marketing, Utility, Authentication, Service }

/// <summary>
/// La tarifa de Meta por mensaje entregado, según el país del destinatario y la categoría, vigente desde una fecha (decisión 9
/// de la E11). Es un dato de referencia: la carga un seed versionado con la tabla oficial de Meta.
/// </summary>
public sealed class WhatsAppRate : Entity
{
    private WhatsAppRate() { }

    public string CountryCode { get; private set; } = string.Empty;
    public WhatsAppPricingCategory Category { get; private set; }
    public decimal Amount { get; private set; }
    public string CurrencyCode { get; private set; } = string.Empty;
    public DateOnly EffectiveFrom { get; private set; }

    public Money Price => new(Amount, ValueObjects.CurrencyCode.Create(CurrencyCode).Value);

    public static WhatsAppRate Create(string countryCode, WhatsAppPricingCategory category, Money price, DateOnly effectiveFrom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentNullException.ThrowIfNull(price);
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        if (price.Amount < 0) throw new ArgumentOutOfRangeException(nameof(price));
        return new WhatsAppRate
        {
            CountryCode = countryCode.Trim().ToUpperInvariant(), Category = category, Amount = price.Amount, CurrencyCode = price.Currency.Value,
            EffectiveFrom = effectiveFrom,
        };
    }
}
