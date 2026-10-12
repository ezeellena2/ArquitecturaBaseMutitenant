using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// La salida de un controller (ADR 0002, enmienda del 2026-10-03): un modelo de Application o, cuando la respuesta
/// lleva value objects o es de protocolo, un <c>*HttpResponse</c> de <c>Api/Contracts</c>. Así una respuesta del Api
/// no se confunde con su homónima de Application, y ningún controller devuelve una entidad ni un tipo de
/// Infrastructure. Mira los tipos que declara cada acción con <c>ProducesResponseType</c>, que son los del OpenAPI.
/// </summary>
public sealed partial class ControllerOutputContractTests
{
    private const string ContractsNamespace = "ArquitecturaBaseMultitenant.Api.Contracts";
    private const string ModelsNamespace = "ArquitecturaBaseMultitenant.Application.Models";

    private static readonly Assembly ApiAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Api");
    private static readonly Assembly ApplicationAssembly = typeof(PagedResult<>).Assembly;

    private static readonly Type[] Wrappers =
    [
        typeof(PagedResult<>), typeof(CursorResult<>), typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>),
        typeof(IEnumerable<>), typeof(List<>),
    ];

    [Fact]
    public void Controllers_return_application_responses_or_http_responses()
    {
        var offenders = ApiAssembly.GetTypes().Where(IsController)
            .SelectMany(DeclaredOutputs)
            .Where(output => !IsAllowedOutput(output.Type))
            .Select(output => $"{output.Action}: {output.Type.FullName}")
            .Distinct().Order(StringComparer.Ordinal).ToArray();

        Assert.True(offenders.Length == 0,
            "Return an Application model or a *HttpResponse from Api/Contracts: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Detection_finds_a_contract_response_without_the_http_suffix_and_accepts_the_allowed_ones()
    {
        Assert.Equal([typeof(ProbeResponse)],
            DeclaredOutputs(typeof(ProbeController)).Select(output => output.Type).Where(type => !IsAllowedOutput(type)));
        Assert.True(IsAllowedOutput(typeof(PagedResult<ProbeHttpResponse>)));
        Assert.True(IsAllowedOutput(typeof(IReadOnlyList<Guid>)));
        Assert.True(IsAllowedOutput(typeof(ArquitecturaBaseMultitenant.Application.Models.Roles.RoleResponse)));
        Assert.False(IsAllowedOutput(typeof(CursorResult<ProbeResponse>)));
        Assert.False(IsAllowedOutput(typeof(ArquitecturaBaseMultitenant.Domain.Authorization.Role)));
        Assert.False(IsAllowedOutput(typeof(ArquitecturaBaseMultitenant.Application.Common.Validation.ValidationRules)));
        Assert.True(IsModuleNamespace("ArquitecturaBaseMultitenant.Application.Modules.Example.Models", "Application", "Models"));
        Assert.False(IsModuleNamespace("ArquitecturaBaseMultitenant.Application.Modules.Example.Services", "Application", "Models"));
        Assert.False(IsModuleNamespace("ArquitecturaBaseMultitenant.Application.Modules.Example.ModelsExtra", "Application", "Models"));
        Assert.False(IsModuleNamespace("ArquitecturaBaseMultitenant.Application.Modules.Example.Sub.Models", "Application", "Models"));
    }

    [Fact]
    public void Detection_includes_invalid_outputs_in_inherited_controller_actions()
    {
        Assert.Equal([typeof(ProbeResponse)], DeclaredOutputs(typeof(InheritedProbeController))
            .Select(output => output.Type).Where(type => !IsAllowedOutput(type)));
    }

    private static IEnumerable<(string Action, Type Type)> DeclaredOutputs(Type controller) =>
        controller.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.DeclaringType != typeof(object) && !method.IsSpecialName && !method.IsDefined(typeof(NonActionAttribute), inherit: true))
            .SelectMany(method => method.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true)
                .Where(attribute => attribute.Type != typeof(void))
                .Select(attribute => ($"{controller.Name}.{method.Name}", attribute.Type)));

    private static bool IsAllowedOutput(Type type)
    {
        if (type.IsArray) return IsAllowedOutput(type.GetElementType()!);
        if (type.IsGenericType && Wrappers.Contains(type.GetGenericTypeDefinition()))
            return type.GenericTypeArguments.All(IsAllowedOutput);
        var value = Nullable.GetUnderlyingType(type) ?? type;
        if (value.IsPrimitive || value.IsEnum || value == typeof(string) || value == typeof(Guid)
            || typeof(ProblemDetails).IsAssignableFrom(value))
            return true;
        if (value.Assembly == ApplicationAssembly)
            return value.Namespace?.StartsWith(ModelsNamespace + ".", StringComparison.Ordinal) == true
                || IsModuleNamespace(value.Namespace, "Application", "Models");
        return IsContract(value) && value.Name.EndsWith("HttpResponse", StringComparison.Ordinal);
    }

    private static bool IsContract(Type type) =>
        (type.Assembly == ApiAssembly || type.DeclaringType == typeof(ControllerOutputContractTests))
        && (type.Namespace?.StartsWith(ContractsNamespace + ".", StringComparison.Ordinal) == true
            || IsModuleNamespace(type.Namespace, "Api", "Contracts")
            || type.DeclaringType == typeof(ControllerOutputContractTests));

    private static bool IsModuleNamespace(string? value, string layer, string folder) =>
        System.Text.RegularExpressions.Regex.IsMatch(value ?? "",
            "^ArquitecturaBaseMultitenant\\." + layer + "\\.Modules\\.[A-Za-z][A-Za-z0-9]*\\." + folder + "(?:\\.|$)");

    private static bool IsController(Type type) =>
        type is { IsClass: true, IsAbstract: false } && typeof(ControllerBase).IsAssignableFrom(type);

    private sealed record ProbeResponse(string Value);

    private sealed record ProbeHttpResponse(string Value);

    private abstract class ProbeControllerBase : ControllerBase
    {
        [HttpGet]
        [ProducesResponseType<ProbeResponse>(200)]
        public OkResult Read() => Ok();
    }

    private sealed class InheritedProbeController : ProbeControllerBase;

    private sealed class ProbeController : ControllerBase
    {
        [HttpGet("probe")]
        [ProducesResponseType<ProbeResponse>(200)]
        [ProducesResponseType<PagedResult<ProbeHttpResponse>>(201)]
        [ProducesResponseType<ProblemDetails>(400)]
        [ProducesResponseType(204)]
        public OkResult Read() => Ok();
    }
}
