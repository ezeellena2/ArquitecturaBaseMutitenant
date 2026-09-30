using ArquitecturaBaseMultitenant.Application.Models.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Services;

/// <summary>Reúne los cinco catálogos globales, traduce sus filas y ofrece búsquedas para selectores y valores ya guardados.</summary>
public interface IReferenceDataService
{
    Task<Result<ReferenceDataResponse>> GetAllAsync(string? culture, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<CurrencyReferenceItem>>> GetCurrenciesAsync(
        string? culture, string? search, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<CountryReferenceItem>>> GetCountriesAsync(
        string? culture, string? search, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TimeZoneReferenceItem>>> GetTimeZonesAsync(
        string? culture, string? search, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<CultureReferenceItem>>> GetCulturesAsync(
        string? culture, string? search, CancellationToken cancellationToken);

    Task<Result<IReadOnlyList<TaxIdTypeReferenceItem>>> GetTaxIdTypesAsync(
        string? culture, string? search, CancellationToken cancellationToken);
}
