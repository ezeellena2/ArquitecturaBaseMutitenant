using System.Reflection;
using ArquitecturaBaseMultitenant.ArchitectureTests.Support;
using Microsoft.Extensions.AI;
using NetArchTest.Rules;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>ADR 0048: AI.Abstractions pertenece al módulo Assistant; SDK y PDF pertenecen a Infrastructure.</summary>
public sealed class AssistantAiBoundaryTests
{
    private const string AssistantNamespace = "ArquitecturaBaseMultitenant.Application.Modules.Assistant";

    [Fact]
    public void Only_the_assistant_module_uses_ai_abstractions_in_application()
    {
        var assembly = Assembly.Load("ArquitecturaBaseMultitenant.Application");
        var outside = assembly.GetTypes().Where(type => !IsAssistant(type.Namespace)).ToArray();
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOn("Microsoft.Extensions.AI")
            .GetResult();
        var failingNames = (result.FailingTypeNames ?? []).Select(name => name.Replace('/', '+'))
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(outside, type => failingNames.Contains(type.FullName!));

        // El IL incluye llamadas y argumentos genéricos incluso cuando no aparecen en una firma.
        Assert.DoesNotContain(CallSites.TypeUses(assembly), use =>
            use.Type.StartsWith("Microsoft.Extensions.AI.", StringComparison.Ordinal)
            && !use.Owner.StartsWith(AssistantNamespace + ".", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Domain")]
    [InlineData("Application")]
    [InlineData("Api")]
    public void Provider_sdk_and_pdf_types_do_not_escape_infrastructure(string layer)
    {
        var result = Types.InAssembly(Assembly.Load("ArquitecturaBaseMultitenant." + layer))
            .ShouldNot().HaveDependencyOnAny("Anthropic", "PdfSharp", "MigraDoc")
            .GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [InlineData("Domain")]
    [InlineData("Api")]
    public void Domain_and_http_contracts_do_not_use_ai_types(string layer)
    {
        var result = Types.InAssembly(Assembly.Load("ArquitecturaBaseMultitenant." + layer))
            .ShouldNot().HaveDependencyOn("Microsoft.Extensions.AI").GetResult();
        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Detector_rejects_ai_in_signatures_generics_and_method_bodies()
    {
        var assembly = typeof(AssistantAiBoundaryTests).Assembly;
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOn("Microsoft.Extensions.AI")
            .GetResult();
        Assert.Contains(typeof(AiSignatureProbe).FullName!, result.FailingTypeNames ?? []);
        Assert.Contains(typeof(AiGenericProbe).FullName!, result.FailingTypeNames ?? []);
        Assert.Contains(CallSites.TypeUses(assembly), use => use.Owner == typeof(AiBodyProbe).FullName
            && use.Type.StartsWith("Microsoft.Extensions.AI.", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(AssistantNamespace, true)]
    [InlineData(AssistantNamespace + ".Tools", true)]
    [InlineData(AssistantNamespace + "Extra", false)]
    [InlineData("ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services", false)]
    [InlineData("ArquitecturaBaseMultitenant.Application.Interfaces.Integrations", false)]
    public void Namespace_boundary_accepts_only_assistant_and_its_children(string value, bool expected) =>
        Assert.Equal(expected, IsAssistant(value));

    private static bool IsAssistant(string? value) => value == AssistantNamespace
        || value?.StartsWith(AssistantNamespace + ".", StringComparison.Ordinal) == true;
}

#pragma warning disable CA1812 // Controles negativos compilados, nunca registros productivos.
internal sealed class AiSignatureProbe
{
    public IChatClient? Client { get; init; }
}

internal sealed class AiGenericProbe
{
    public IReadOnlyList<IChatClient>? Clients { get; init; }
}

internal sealed class AiBodyProbe
{
#pragma warning disable CA1859 // La firma sin AI prueba la detección de una referencia solamente en el cuerpo.
    public static object Create() => new ChatOptions();
#pragma warning restore CA1859
}
#pragma warning restore CA1812
