using System.Globalization;
using System.Resources;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Restricciones del contrato del proveedor, independientes de los catálogos de selección.</summary>
public static class AssistantContractPolicy
{
    private static readonly ResourceManager Data = new("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources.AssistantContractData", typeof(AssistantContractPolicy).Assembly);
    public static string BillingCurrency => Data.GetString(nameof(BillingCurrency), CultureInfo.InvariantCulture)
        ?? throw new InvalidOperationException("The billing currency contract is missing.");
    public static bool IsValidBudget(Money? value) => value is not null && value.Currency.Value == BillingCurrency
        && value.Amount >= 0 && value.Amount < 1_000_000_000_000m;
    public static bool IsValidTariff(Money? value) => IsValidBudget(value) && decimal.Round(value!.Amount, 6) == value.Amount;
}
