using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Api.RateLimiting;

/// <summary>Define cupos y ventanas por IP para códigos de ingreso y aceptación de invitaciones.</summary>
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

    [Range(1, 100_000)]
    public int InvitationPreviewPermitLimit { get; init; } = 60;

    [Range(1, 1440)]
    public int InvitationPreviewWindowMinutes { get; init; } = 15;

    [Range(1, 100_000)]
    public int InvitationAcceptPermitLimit { get; init; } = 10;

    [Range(1, 1440)]
    public int InvitationAcceptWindowMinutes { get; init; } = 15;
}
