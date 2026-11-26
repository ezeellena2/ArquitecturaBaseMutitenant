using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Models.Features;
using ArquitecturaBaseMultitenant.Application.Services.Features;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Services.Features;

/// <summary>
/// El resolvedor da el tope de cada clave que declara un módulo (ADR 0069 §4): en esta etapa, el máximo de la
/// plataforma, también sin organización. Una clave desconocida o repetida, o un aporte que devuelve la otra clase, son
/// bugs y tiran una excepción. Las declaraciones reales tienen claves únicas con el prefijo de una feature de su
/// módulo, y mínimo y entrada coherentes.
/// </summary>
public sealed class ModuleLimitResolverTests
{
    private const string ModulesNamespace = "ArquitecturaBaseMultitenant.Application.Modules.";
    private static readonly Assembly ApplicationAssembly = typeof(IModuleLimitContributor).Assembly;
    private static readonly Money Budget = new(250m, CurrencyCode.Create("USD").Value);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Gives_the_platform_maximum_with_or_without_an_organization()
    {
        var contributor = new FixtureContributor(
            [new("fixture.days", ModuleLimitKind.Count, 90, 1), new("fixture.budget", ModuleLimitKind.Amount, null, null)],
            new Dictionary<string, ModuleLimitValue>
            {
                ["fixture.days"] = new(30, null),
                ["fixture.budget"] = new(null, Budget),
            });
        var resolver = new ModuleLimitResolver([contributor]);

        var forOrganization = await resolver.ResolveAsync("fixture.days", Guid.NewGuid(), Ct);
        var withoutOrganization = await resolver.ResolveAsync("fixture.days", null, Ct);
        var amount = await resolver.ResolveAsync("fixture.budget", null, Ct);

        Assert.Equal(("fixture.days", 30, (Money?)null), (forOrganization.Key, forOrganization.Count, forOrganization.Amount));
        Assert.Equal(forOrganization, withoutOrganization);
        Assert.Equal(("fixture.budget", (int?)null, Budget), (amount.Key, amount.Count, amount.Amount));
        Assert.Equal(["fixture.days", "fixture.days", "fixture.budget"], contributor.Reads);
    }

    [Fact]
    public async Task An_unknown_key_is_a_bug()
    {
        var contributor = new FixtureContributor([new("fixture.days", ModuleLimitKind.Count, 90, 1)],
            new Dictionary<string, ModuleLimitValue> { ["fixture.days"] = new(30, null) });
        var resolver = new ModuleLimitResolver([contributor]);

        await Assert.ThrowsAsync<ArgumentException>(() => resolver.ResolveAsync("fixture.other", null, Ct));
        await Assert.ThrowsAsync<ArgumentException>(() => resolver.ResolveAsync("FIXTURE.DAYS", null, Ct));
        Assert.Empty(contributor.Reads);
    }

    [Theory]
    [InlineData(ModuleLimitKind.Count, false, true)]
    [InlineData(ModuleLimitKind.Count, false, false)]
    [InlineData(ModuleLimitKind.Count, true, true)]
    [InlineData(ModuleLimitKind.Amount, true, false)]
    [InlineData(ModuleLimitKind.Amount, false, false)]
    [InlineData(ModuleLimitKind.Amount, true, true)]
    public async Task A_maximum_of_the_other_class_is_a_bug(ModuleLimitKind kind, bool withCount, bool withAmount)
    {
        var definition = kind == ModuleLimitKind.Count
            ? new ModuleLimitDefinition("fixture.limit", kind, 90, 1)
            : new ModuleLimitDefinition("fixture.limit", kind, null, null);
        var resolver = new ModuleLimitResolver([new FixtureContributor([definition],
            new Dictionary<string, ModuleLimitValue> { ["fixture.limit"] = new(withCount ? 30 : null, withAmount ? Budget : null) })]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveAsync("fixture.limit", Guid.NewGuid(), Ct));
    }

    [Fact]
    public void A_key_declared_twice_is_a_bug()
    {
        var first = new FixtureContributor([new("fixture.days", ModuleLimitKind.Count, 90, 1)], new Dictionary<string, ModuleLimitValue>());
        var second = new FixtureContributor([new("fixture.days", ModuleLimitKind.Count, null, 0)], new Dictionary<string, ModuleLimitValue>());
        var twiceInOne = new FixtureContributor(
            [new("fixture.hours", ModuleLimitKind.Count, null, 0), new("fixture.hours", ModuleLimitKind.Count, null, 0)],
            new Dictionary<string, ModuleLimitValue>());

        Assert.Throws<InvalidOperationException>(() => new ModuleLimitResolver([first, second]));
        Assert.Throws<InvalidOperationException>(() => new ModuleLimitResolver([twiceInOne]));
    }

    [Theory]
    [InlineData(10, 90, 30, 10)]
    [InlineData(50, 90, 30, 30)]
    [InlineData(30, 90, 30, 30)]
    [InlineData(null, 90, 30, 30)]
    [InlineData(null, 20, 30, 20)]
    [InlineData(null, null, 30, 30)]
    [InlineData(5, null, 30, 5)]
    public async Task Effective_is_the_lower_of_the_choice_or_the_default_and_the_ceiling(
        int? organizationValue, int? defaultValue, int maximum, int expected)
    {
        var resolver = new ModuleLimitResolver([new FixtureContributor([new("fixture.days", ModuleLimitKind.Count, defaultValue, 1)],
            new Dictionary<string, ModuleLimitValue> { ["fixture.days"] = new(maximum, null) })]);

        var ceiling = await resolver.ResolveAsync("fixture.days", Guid.NewGuid(), Ct);

        Assert.Equal(expected, ceiling.Effective(organizationValue));
    }

    [Fact]
    public void Effective_of_an_amount_ceiling_is_a_bug() =>
        Assert.Throws<InvalidOperationException>(() => new ModuleLimitCeiling("fixture.budget", null, Budget).Effective(10));

    [Fact]
    public void Real_declarations_have_unique_keys_prefixed_by_a_feature_of_their_module_and_a_coherent_minimum_and_default()
    {
        var declarations = ApplicationAssembly.GetTypes()
            .Where(type => IsConcrete(type, typeof(IModuleLimitContributor)))
            .Select(type => new Declaration(ModuleOf(type), Instantiate<IModuleLimitContributor>(type).Limits))
            .ToArray();

        Assert.Empty(DeclarationProblems(declarations, FeaturesByModule()));
    }

    [Fact]
    public void Detector_rejects_incoherent_declarations()
    {
        var features = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["Fixture"] = new HashSet<string>(["fixture"], StringComparer.Ordinal),
            ["Other"] = new HashSet<string>(["other"], StringComparer.Ordinal),
        };
        ModuleLimitDefinition[] valid =
        [
            new("fixture.days", ModuleLimitKind.Count, 90, 1),
            new("fixture.hours", ModuleLimitKind.Count, null, 0),
            new("fixture.budget", ModuleLimitKind.Amount, null, null),
        ];

        Assert.Empty(DeclarationProblems([new("Fixture", valid)], features));
        Assert.All(new ModuleLimitDefinition[][]
        {
            [new("other.days", ModuleLimitKind.Count, 90, 1)],
            [new("unknown.days", ModuleLimitKind.Count, 90, 1)],
            [new("fixture", ModuleLimitKind.Count, 90, 1)],
            [new("fixture.", ModuleLimitKind.Count, 90, 1)],
            [new("fixture.days", ModuleLimitKind.Count, 90, null)],
            [new("fixture.days", ModuleLimitKind.Count, 90, -1)],
            [new("fixture.days", ModuleLimitKind.Count, 0, 1)],
            [new("fixture.budget", ModuleLimitKind.Amount, 10, null)],
            [new("fixture.budget", ModuleLimitKind.Amount, null, 0)],
            [new("fixture.days", ModuleLimitKind.Count, 90, 1), new("fixture.days", ModuleLimitKind.Count, 90, 1)],
        }, limits => Assert.NotEmpty(DeclarationProblems([new("Fixture", limits)], features)));
        Assert.NotEmpty(DeclarationProblems([new(null, [new("fixture.days", ModuleLimitKind.Count, 90, 1)])], features));
        Assert.NotEmpty(DeclarationProblems(
            [new("Fixture", [new("fixture.days", ModuleLimitKind.Count, 90, 1)]), new("Fixture", [new("fixture.days", ModuleLimitKind.Count, 30, 1)])],
            features));
    }

    private sealed record Declaration(string? Module, IReadOnlyList<ModuleLimitDefinition> Limits);

    /// <summary>Lo que está mal en las declaraciones: una clave repetida, sin la feature de su módulo como prefijo, o sin coherencia entre la clase, el mínimo y la entrada.</summary>
    private static IEnumerable<string> DeclarationProblems(
        IReadOnlyList<Declaration> declarations, Dictionary<string, IReadOnlySet<string>> featuresByModule)
    {
        var repeated = declarations.SelectMany(declaration => declaration.Limits)
            .GroupBy(limit => limit.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: declared more than once");
        var incoherent = declarations.SelectMany(declaration => declaration.Limits
            .Select(limit => LimitProblem(limit, declaration.Module is not null && featuresByModule.TryGetValue(declaration.Module, out var keys) ? keys : null))
            .OfType<string>());
        return [.. repeated, .. incoherent];
    }

    private static string? LimitProblem(ModuleLimitDefinition limit, IReadOnlySet<string>? moduleFeatures)
    {
        var separator = limit.Key.LastIndexOf('.');
        if (separator <= 0 || separator == limit.Key.Length - 1 || moduleFeatures?.Contains(limit.Key[..separator]) != true)
            return $"{limit.Key}: the key is not <feature of its module>.<field>";
        return limit.Kind switch
        {
            ModuleLimitKind.Count when limit.Minimum is not >= 0 => $"{limit.Key}: a Count needs a minimum of 0 or more",
            ModuleLimitKind.Count when limit.Default < limit.Minimum => $"{limit.Key}: the default is below the minimum",
            ModuleLimitKind.Amount when limit.Default is not null || limit.Minimum is not null => $"{limit.Key}: an Amount has no default or minimum",
            _ => null,
        };
    }

    /// <summary>Las features que declara cada módulo con su <see cref="IFeatureDefinitionContributor"/>.</summary>
    private static Dictionary<string, IReadOnlySet<string>> FeaturesByModule() => ApplicationAssembly.GetTypes()
        .Where(type => IsConcrete(type, typeof(IFeatureDefinitionContributor)) && ModuleOf(type) is not null)
        .GroupBy(type => ModuleOf(type)!, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => (IReadOnlySet<string>)group
            .SelectMany(type => Instantiate<IFeatureDefinitionContributor>(type).Definitions)
            .Select(definition => definition.Key)
            .ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

    private static bool IsConcrete(Type type, Type contract) =>
        type is { IsClass: true, IsAbstract: false } && contract.IsAssignableFrom(type);

    private static string? ModuleOf(Type type) => type.Namespace?.StartsWith(ModulesNamespace, StringComparison.Ordinal) == true
        ? type.Namespace[ModulesNamespace.Length..].Split('.')[0]
        : null;

    /// <summary>
    /// Lo que declara un aporte no depende de lo que recibe: se arma con sus dependencias vacías, que la declaración no
    /// usa. Leer el máximo sí las necesita, y eso lo prueba cada módulo.
    /// </summary>
    private static T Instantiate<T>(Type type)
    {
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null)
            .ToArray();
        return (T)constructor.Invoke(arguments);
    }

    private sealed class FixtureContributor(IReadOnlyList<ModuleLimitDefinition> limits, IReadOnlyDictionary<string, ModuleLimitValue> maxima)
        : IModuleLimitContributor
    {
        internal List<string> Reads { get; } = [];

        public IReadOnlyList<ModuleLimitDefinition> Limits => limits;

        public Task<ModuleLimitValue> ReadPlatformMaximumAsync(string key, CancellationToken cancellationToken)
        {
            Reads.Add(key);
            return Task.FromResult(maxima[key]);
        }
    }
}
