using System.Reflection;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Modules.Control.Services;
using ArquitecturaBaseMultitenant.ArchitectureTests.Support;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// ADR 0069 §4 y docs/rules/modulos-habilitados.md («Topes»): cada tope con máximo de la plataforma lo declara el aporte
/// (<see cref="IModuleLimitContributor"/>) de su módulo, y su máximo lo lee solo ese aporte, así que todo uso pasa por
/// <c>ModuleLimitResolver</c>. La única excepción es la pantalla de Plataforma del módulo, que lo muestra para editarlo,
/// nombrada con su motivo. Ningún módulo nombra la configuración de otro: ni sus tipos ni las claves de sus topes. Se lee
/// el IL con <see cref="CallSites"/>: una <c>const</c> de clave se copia como literal en quien la usa.
/// </summary>
public sealed class ModuleLimitArchitectureTests
{
    private const string Assistant = "ArquitecturaBaseMultitenant.Application.Modules.Assistant";
    private const string WhatsApp = "ArquitecturaBaseMultitenant.Application.Modules.WhatsApp";
    private const string AssistantPlatformSettings = Assistant + ".Models.PlatformAssistantSettingsResponse";
    private const string WhatsAppPlatformSettings = WhatsApp + ".Interfaces.Persistence.WhatsAppPlatformSettingsRow";

    private static readonly Assembly[] Production =
    [
        Assembly.Load("ArquitecturaBaseMultitenant.Domain"),
        Assembly.Load("ArquitecturaBaseMultitenant.Application"),
        Assembly.Load("ArquitecturaBaseMultitenant.Infrastructure"),
        Assembly.Load("ArquitecturaBaseMultitenant.Api"),
    ];

    // La lista cerrada: de cada clave que declara un módulo, el getter de su máximo de la plataforma, el aporte que la declara
    // y lo lee, y las clases de la pantalla de Plataforma de su módulo que también lo leen, con su motivo. Un tope nuevo se
    // agrega acá, en el aporte de su módulo y en la ficha.
    private static readonly PlatformMaximum[] Maximums =
    [
        new("assistant.dailyMessages", AssistantPlatformSettings, "get_DailyMessageLimit",
            Assistant + ".Services.AssistantModuleLimitContributor", new Dictionary<string, string>()),
        new("assistant.monthlyBudget", AssistantPlatformSettings, "get_MonthlyOrganizationBudget",
            Assistant + ".Services.AssistantModuleLimitContributor", new Dictionary<string, string>()),
        new("whatsapp.chatRetentionDays", WhatsAppPlatformSettings, "get_MaxOrganizationChatRetentionDays",
            WhatsApp + ".Services.WhatsAppModuleLimitContributor", new Dictionary<string, string>
            {
                [WhatsApp + ".Services.PlatformWhatsAppSettingsService"] =
                    "su GET devuelve el máximo guardado para que Plataforma lo vea y lo edite; no lo usa como tope",
            }),
    ];

    private static readonly Regex ModuleNamespace = new(
        @"^ArquitecturaBaseMultitenant\.[^.]+\.Modules\.([A-Za-z][A-Za-z0-9]*)(?:\.|$)", RegexOptions.CultureInvariant);

    // Los módulos que existen, por sus carpetas Modules; su dominio vive en Domain.<Módulo>.
    private static readonly HashSet<string> ModuleNames = Production.SelectMany(assembly => assembly.GetTypes())
        .Select(type => ModuleNamespace.Match(type.FullName ?? ""))
        .Where(match => match.Success)
        .Select(match => match.Groups[1].Value)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Every_declared_limit_has_its_listed_platform_maximum_and_every_listed_one_is_declared()
    {
        Assert.Equal(
            Maximums.Select(maximum => $"{maximum.Key} <- {maximum.Contributor}").Order(StringComparer.Ordinal),
            Declarations().Select(limit => $"{limit.Key} <- {limit.Contributor}").Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// La igualdad va en los dos sentidos: un lector de más es un tope que no pasa por el resolvedor, y un aporte que no
    /// aparece es un getter equivocado en la lista (el aporte lee su máximo de otro lado).
    /// </summary>
    [Fact]
    public void Each_platform_maximum_is_read_only_by_its_contributor_and_its_platform_screen()
    {
        var expected = Maximums
            .SelectMany(maximum => maximum.PlatformScreen.Keys.Append(maximum.Contributor)
                .Select(reader => $"{maximum.Key}: {reader}"))
            .ToArray();
        var actual = Readers(Production.SelectMany(CallSites.Calls)).ToArray();

        var unlisted = actual.Except(expected, StringComparer.Ordinal).ToArray();
        var missing = expected.Except(actual, StringComparer.Ordinal).ToArray();

        Assert.True(unlisted.Length == 0, "Leen un máximo sin pasar por el resolvedor:" + Lines(unlisted));
        Assert.True(missing.Length == 0, "Listados que no lo leen:" + Lines(missing));
    }

    [Fact]
    public void Platform_screen_readers_belong_to_the_module_and_carry_their_reason()
    {
        Assert.All(Maximums.SelectMany(maximum => maximum.PlatformScreen.Select(reader => (maximum.Contributor, reader))), entry =>
        {
            Assert.NotNull(ModuleOf(entry.reader.Key));
            Assert.Equal(ModuleOf(entry.Contributor), ModuleOf(entry.reader.Key));
            Assert.False(string.IsNullOrWhiteSpace(entry.reader.Value));
        });
    }

    [Fact]
    public void No_module_names_the_configuration_of_another()
    {
        var foreign = ForeignConfiguration(Production).ToArray();

        Assert.True(foreign.Length == 0, "Nombran la configuración de otro módulo:" + Lines(foreign));
    }

    [Fact]
    public void Detector_catches_a_module_that_reads_a_maximum_or_names_the_configuration_of_another()
    {
        var probe = typeof(ControlForeignLimitReader);
        var readers = Readers(CallSites.Calls(probe.Assembly)).ToArray();
        var foreign = ForeignConfiguration([probe.Assembly]).ToArray();

        Assert.Contains($"assistant.dailyMessages: {probe.FullName}", readers);
        Assert.Contains($"whatsapp.chatRetentionDays: {probe.FullName}", readers);
        Assert.Contains($"{probe.FullName} -> {WhatsAppPlatformSettings}", foreign);
        Assert.Contains($"{probe.FullName} -> {AssistantPlatformSettings}", foreign);
        Assert.Contains($"{probe.FullName} -> \"whatsapp.chatRetentionDays\"", foreign);
    }

    [Theory]
    [InlineData(WhatsApp + ".Services.WhatsAppSettingsResolver", "WhatsApp")]
    [InlineData("ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Persistence.Readers.AssistantSettingsReader", "Assistant")]
    [InlineData("ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppPlatformSettings", "WhatsApp")]
    [InlineData("ArquitecturaBaseMultitenant.Application.Services.Features.ModuleLimitResolver", null)]
    [InlineData("ArquitecturaBaseMultitenant.Domain.Features.Features", null)]
    public void Module_of_a_type_comes_from_its_modules_folder_or_its_domain(string type, string? module) =>
        Assert.Equal(module, ModuleOf(type));

    private static IEnumerable<(string Key, string Contributor)> Declarations() =>
        Production.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IModuleLimitContributor).IsAssignableFrom(type))
            .SelectMany(type => Create(type).Limits.Select(limit => (limit.Key, type.FullName!)));

    // Limits es una lista fija que no usa las dependencias del aporte: se arma con nulos.
    private static IModuleLimitContributor Create(Type type)
    {
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        return (IModuleLimitContributor)constructor.Invoke(new object?[constructor.GetParameters().Length]);
    }

    // Quien lee el getter de cada máximo, salvo el propio tipo (un record se lee a sí mismo en PrintMembers y Deconstruct).
    private static IEnumerable<string> Readers(IEnumerable<CallSites.Call> calls)
    {
        var all = calls.ToArray();
        return Maximums
            .SelectMany(maximum => all
                .Where(call => call.DeclaringType == maximum.Type && call.Method == maximum.Getter && call.Owner != maximum.Type)
                .Select(call => $"{maximum.Key}: {call.Owner}"))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
    }

    // Un tipo de configuración de otro módulo (su Settings o sus Options), o la clave de un tope que declara otro módulo. La
    // clave tampoco la nombra el núcleo: la lee el resolvedor por el aporte, sin nombrarla.
    private static IEnumerable<string> ForeignConfiguration(IReadOnlyCollection<Assembly> assemblies)
    {
        var keys = Declarations().ToDictionary(limit => limit.Key, limit => ModuleOf(limit.Contributor), StringComparer.Ordinal);
        var types = assemblies.SelectMany(CallSites.TypeUses)
            .Where(use => IsConfiguration(use.Type) && ModuleOf(use.Owner) is { } owner && ModuleOf(use.Type) is { } target
                && owner != target)
            .Select(use => $"{use.Owner} -> {use.Type}");
        var literals = assemblies.SelectMany(CallSites.Literals)
            .Where(literal => keys.TryGetValue(literal.Value, out var module) && ModuleOf(literal.Owner) != module)
            .Select(literal => $"{literal.Owner} -> \"{literal.Value}\"");
        return types.Concat(literals).Distinct(StringComparer.Ordinal);
    }

    private static string Lines(IEnumerable<string> values) =>
        string.Concat(values.Select(value => Environment.NewLine + value));

    private static bool IsConfiguration(string type)
    {
        var name = type[(type.LastIndexOfAny(['.', '/']) + 1)..];
        return name.Contains("Settings", StringComparison.Ordinal) || name.EndsWith("Options", StringComparison.Ordinal);
    }

    private static string? ModuleOf(string type)
    {
        var match = ModuleNamespace.Match(type);
        if (match.Success)
            return match.Groups[1].Value;
        var parts = type.Split('.');
        return parts is ["ArquitecturaBaseMultitenant", "Domain", var module, _, ..] && ModuleNames.Contains(module) ? module : null;
    }

    private sealed record PlatformMaximum(string Key, string Type, string Getter, string Contributor,
        IReadOnlyDictionary<string, string> PlatformScreen);
}
