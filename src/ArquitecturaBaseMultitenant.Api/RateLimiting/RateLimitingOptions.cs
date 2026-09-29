using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Api.RateLimiting;

internal sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    [Range(1, 100_000)]
    public int LoginCodePermitLimit { get; init; } = 20;

    [Range(1, 1440)]
    public int LoginCodeWindowMinutes { get; init; } = 15;

    [Range(1, 100_000)]
    public int LoginVerifyPermitLimit { get; init; } = 30;

    [Range(1, 1440)]
    public int LoginVerifyWindowMinutes { get; init; } = 15;
}
