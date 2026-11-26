using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Models.Features;

namespace ArquitecturaBaseMultitenant.Application.Services.Features;

/// <summary>
/// Da el tope de cada clave que declara un módulo con su <see cref="IModuleLimitContributor"/> (ADR 0069 §4), sin nombrar
/// módulos. En esta etapa el tope es el máximo de la plataforma, que el aporte lee donde se lee hoy, en cada uso y sin
/// caché (270); la etapa del cobro le suma el valor que fije Plataforma para una organización y el del plan. Una clave
/// desconocida o repetida, o un máximo de la otra clase, son bugs: tiran una excepción.
/// </summary>
internal sealed class ModuleLimitResolver
{
    private readonly Dictionary<string, (IModuleLimitContributor Contributor, ModuleLimitDefinition Definition)> limits =
        new(StringComparer.Ordinal);

    public ModuleLimitResolver(IEnumerable<IModuleLimitContributor> contributors)
    {
        foreach (var contributor in contributors)
            foreach (var definition in contributor.Limits)
                if (!limits.TryAdd(definition.Key, (contributor, definition)))
                    throw new InvalidOperationException($"Module limit key '{definition.Key}' is declared more than once.");
    }

    /// <summary>
    /// El tope de <paramref name="key"/> para una organización. Sin organización (una persona o Plataforma), y en esta
    /// etapa también con ella, es el máximo de la plataforma.
    /// </summary>
    internal async Task<ModuleLimitCeiling> ResolveAsync(string key, Guid? organizationId, CancellationToken ct)
    {
        _ = organizationId;
        if (!limits.TryGetValue(key, out var limit))
            throw new ArgumentException($"Module limit key '{key}' is not declared by any module.", nameof(key));
        var maximum = await limit.Contributor.ReadPlatformMaximumAsync(key, ct);
        if (!HasOnly(limit.Definition.Kind, maximum))
            throw new InvalidOperationException($"Module limit '{key}' is a {limit.Definition.Kind} and its platform maximum is not.");
        return new ModuleLimitCeiling(key, maximum.Count, maximum.Amount) { Default = limit.Definition.Default };
    }

    private static bool HasOnly(ModuleLimitKind kind, ModuleLimitValue value) => kind switch
    {
        ModuleLimitKind.Count => value is { Count: not null, Amount: null },
        ModuleLimitKind.Amount => value is { Count: null, Amount: not null },
        _ => false,
    };
}
