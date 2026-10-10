using System.Globalization;
using System.Resources;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

/// <summary>Restricciones del contrato del proveedor, independientes de los catálogos de selección.</summary>
public static class AssistantContractPolicy
{
    private static readonly ResourceManager Data = new("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources.AssistantContractData", typeof(AssistantContractPolicy).Assembly);
    public static string BillingCurrency => Data.GetString(nameof(BillingCurrency), CultureInfo.InvariantCulture)
        ?? throw new InvalidOperationException("The billing currency contract is missing.");
}
