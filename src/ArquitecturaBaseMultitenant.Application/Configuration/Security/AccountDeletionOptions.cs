using System.ComponentModel.DataAnnotations;

namespace ArquitecturaBaseMultitenant.Application.Configuration.Security;

/// <summary>
/// El proceso de bajas vencidas (E9), en AccountDeletion: hasta 100 cuentas y 100 retenciones por ciclo, un ciclo por hora,
/// como siempre.
/// </summary>
public sealed class AccountDeletionOptions
{
    public const string SectionName = "AccountDeletion";

    [Range(1, 10_000)]
    public int BatchSize { get; init; } = 100;

    [Range(1, 1440)]
    public int IntervalMinutes { get; init; } = 60;
}
