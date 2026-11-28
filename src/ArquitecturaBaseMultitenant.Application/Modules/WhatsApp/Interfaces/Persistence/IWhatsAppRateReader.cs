using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Persistence;

/// <summary>Las tarifas de Meta (dato de referencia, decisión 9 de la E11).</summary>
public interface IWhatsAppRateReader
{
    /// <summary>La tarifa por mensaje vigente en esa fecha para el país del destinatario y la categoría; null si no hay.</summary>
    Task<Money?> FindAsync(string countryCode, WhatsAppPricingCategory category, DateOnly date, CancellationToken ct);

    /// <summary>Primera tarifa vigente en esa fecha para la categoría, en orden de país; respaldo si ningún país habilitado tiene tarifa propia.</summary>
    Task<(string CountryCode, Money Price)?> FindFallbackAsync(WhatsAppPricingCategory category, DateOnly date, CancellationToken ct);
}
