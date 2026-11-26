using ArquitecturaBaseMultitenant.Application.Models.Features;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;

/// <summary>
/// Los topes de un módulo que tienen máximo de la plataforma (ADR 0069 §4). Cada módulo declara los suyos y lee su
/// máximo donde se lee hoy; el núcleo los resuelve con ModuleLimitResolver sin nombrar módulos.
/// </summary>
public interface IModuleLimitContributor
{
    IReadOnlyList<ModuleLimitDefinition> Limits { get; }

    /// <summary>El máximo de la plataforma de una clave de <see cref="Limits"/>, cargado según su clase.</summary>
    Task<ModuleLimitValue> ReadPlatformMaximumAsync(string key, CancellationToken cancellationToken);
}
