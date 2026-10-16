using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Conventions;

/// <summary>
/// Las doce cuotas compartidas (E9) son un valor, nunca una entidad: se guardan como jsonb legible con los nombres del
/// contrato, <c>{"ip":{"permitLimit":300,"windowSeconds":60},…}</c>.
/// </summary>
internal static class PlatformRateLimitsConvention
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Configure(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<PlatformRateLimits>()
            .HaveConversion<PlatformRateLimitsValueConverter>()
            .HaveColumnType("jsonb");
    }

    internal static string Write(PlatformRateLimits limits) => JsonSerializer.Serialize(limits, Json);

    internal static PlatformRateLimits Read(string json) => JsonSerializer.Deserialize<PlatformRateLimits>(json, Json)
        ?? throw new InvalidOperationException("The stored rate limits are empty.");

    private sealed class PlatformRateLimitsValueConverter() : ValueConverter<PlatformRateLimits, string>(
        limits => Write(limits),
        json => Read(json));
}
