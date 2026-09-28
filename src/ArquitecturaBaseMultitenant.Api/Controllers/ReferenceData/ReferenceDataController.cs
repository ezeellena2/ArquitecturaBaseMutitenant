using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.Contracts.ReferenceData;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Controllers.ReferenceData;

[ApiController]
[AllowAnonymous]
[Route("api/reference-data")]
[Tags("ReferenceData")]
public sealed class ReferenceDataController(IReferenceDataService service) : ControllerBase
{
    private static readonly JsonSerializerOptions EtagJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    [ProducesResponseType<ReferenceDataHttpResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await service.GetAllAsync(CultureInfo.CurrentUICulture.Name, cancellationToken);
        return ToCachedResult(result, ReferenceDataHttpResponse.FromModel);
    }

    [HttpGet("currencies")]
    [ProducesResponseType<IReadOnlyList<CurrencyReferenceHttpResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetCurrencies([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await service.GetCurrenciesAsync(CultureInfo.CurrentUICulture.Name, search, cancellationToken);
        return ToCachedResult(result, rows => rows.Select(CurrencyReferenceHttpResponse.FromModel).ToArray());
    }

    [HttpGet("countries")]
    [ProducesResponseType<IReadOnlyList<CountryReferenceHttpResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetCountries([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await service.GetCountriesAsync(CultureInfo.CurrentUICulture.Name, search, cancellationToken);
        return ToCachedResult(result, rows => rows.Select(CountryReferenceHttpResponse.FromModel).ToArray());
    }

    [HttpGet("time-zones")]
    [ProducesResponseType<IReadOnlyList<TimeZoneReferenceHttpResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetTimeZones([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await service.GetTimeZonesAsync(CultureInfo.CurrentUICulture.Name, search, cancellationToken);
        return ToCachedResult(result, rows => rows.Select(TimeZoneReferenceHttpResponse.FromModel).ToArray());
    }

    [HttpGet("cultures")]
    [ProducesResponseType<IReadOnlyList<CultureReferenceHttpResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetCultures([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await service.GetCulturesAsync(CultureInfo.CurrentUICulture.Name, search, cancellationToken);
        return ToCachedResult(result, rows => rows.Select(CultureReferenceHttpResponse.FromModel).ToArray());
    }

    [HttpGet("tax-id-types")]
    [ProducesResponseType<IReadOnlyList<TaxIdTypeReferenceHttpResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<IActionResult> GetTaxIdTypes([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var result = await service.GetTaxIdTypesAsync(CultureInfo.CurrentUICulture.Name, search, cancellationToken);
        return ToCachedResult(result, rows => rows.Select(TaxIdTypeReferenceHttpResponse.FromModel).ToArray());
    }

    private IActionResult ToCachedResult<TModel, THttp>(Result<TModel> result, Func<TModel, THttp> map)
    {
        if (result.IsFailure)
        {
            return result.ToActionResult(this);
        }

        var response = map(result.Value);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, EtagJsonOptions);
        var etag = $"\"{Convert.ToHexString(SHA256.HashData(bytes))}\"";

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "public, max-age=0, must-revalidate";
        if (Request.Headers.IfNoneMatch.Any(value => string.Equals(value, etag, StringComparison.Ordinal)))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Result.Success(response).ToActionResult(this);
    }
}
