using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.ValueObjects;

/// <summary>Importe decimal con moneda explícita, sin consultar un catálogo.</summary>
public sealed class Money : ValueObject
{
    public Money(decimal amount, CurrencyCode currency)
    {
        ArgumentNullException.ThrowIfNull(currency);

        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public CurrencyCode Currency { get; }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Round(int minorUnits) =>
        new(decimal.Round(Amount, minorUnits, MidpointRounding.AwayFromZero), Currency);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!Currency.Equals(other.Currency))
        {
            throw new InvalidOperationException("Money in different currencies cannot be combined.");
        }
    }
}
