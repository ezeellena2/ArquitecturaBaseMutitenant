using System.Reflection;
using ArquitecturaBaseMultitenant.Application.Models.Common;
using ArquitecturaBaseMultitenant.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que las ediciones de entidades con versión reciban esa versión en su contrato HTTP. Protege la
/// detección de cambios simultáneos.
/// </summary>
public sealed class VersionedContractTests
{
    [Fact]
    public void Versioned_entity_edits_expose_a_version_in_the_http_contract()
    {
        var versionedEntityNames = typeof(IVersioned).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(IVersioned).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
        var api = Assembly.Load("ArquitecturaBaseMultitenant.Api");
        var offenders = api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(typeof(HttpPutAttribute)) || method.IsDefined(typeof(HttpDeleteAttribute)))
            .SelectMany(method => method.GetParameters()
                .Where(parameter => parameter.ParameterType.Namespace?.StartsWith(
                    "ArquitecturaBaseMultitenant.Api.Contracts", StringComparison.Ordinal) == true)
                .Select(parameter => (Method: method, Contract: parameter.ParameterType)))
            .Where(item => versionedEntityNames.Any(name => item.Contract.Name.Contains(name, StringComparison.Ordinal)))
            .Where(item => !HasVersion(item.Contract))
            .Select(item => $"{item.Method.DeclaringType?.Name}.{item.Method.Name}: {item.Contract.Name}")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Guard_detects_a_missing_version_and_accepts_a_versioned_contract()
    {
        Assert.False(HasVersion(typeof(WithoutVersion)));
        Assert.True(HasVersion(typeof(WithVersion)));
    }

    /// <summary>
    /// Un <c>PUT</c> o <c>POST</c> que recibe <c>version</c> responde 200 con la versión nueva (<see cref="VersionResponse"/>), no
    /// 204: el front la toma como base del próximo guardado, sin relectura (docs/rules/concurrencia.md).
    /// </summary>
    [Fact]
    public void Versioned_saves_answer_the_new_version()
    {
        var api = Assembly.Load("ArquitecturaBaseMultitenant.Api");
        var offenders = api.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(IsVersionedSave)
            .Where(method => !AnswersTheNewVersion(method))
            .Select(method => $"{method.DeclaringType?.Name}.{method.Name}")
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Guard_detects_a_versioned_save_without_the_new_version()
    {
        var actions = typeof(ProbeController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(IsVersionedSave).ToDictionary(method => method.Name);

        Assert.Equal(["Answers", "PostSilent", "Silent"], actions.Keys.Order(StringComparer.Ordinal));
        Assert.True(AnswersTheNewVersion(actions["Answers"]));
        Assert.False(AnswersTheNewVersion(actions["Silent"]));
        Assert.False(AnswersTheNewVersion(actions["PostSilent"]));
    }

    private static bool IsVersionedSave(MethodInfo method) =>
        (method.IsDefined(typeof(HttpPutAttribute)) || method.IsDefined(typeof(HttpPostAttribute)))
        && method.GetParameters().Any(parameter => parameter.ParameterType.Name.EndsWith("HttpRequest", StringComparison.Ordinal)
            && HasVersion(parameter.ParameterType));

    private static bool AnswersTheNewVersion(MethodInfo method)
    {
        var declared = method.GetCustomAttributes<ProducesResponseTypeAttribute>(inherit: true).ToArray();
        return declared.Any(attribute => attribute.StatusCode == StatusCodes.Status200OK
                && attribute.Type == typeof(VersionResponse))
            && declared.All(attribute => attribute.StatusCode != StatusCodes.Status204NoContent);
    }

    private static bool HasVersion(Type contract) =>
        contract.GetProperty("Version") is { PropertyType: var propertyType }
        && (propertyType == typeof(uint) || propertyType == typeof(uint?));

    private sealed record WithoutVersion(string Name);

    private sealed record WithVersion(string Name, uint Version);

    private sealed record ProbeHttpRequest(string Name, uint? Version);

    private sealed class ProbeController : ControllerBase
    {
        [HttpPut("answers")]
        [ProducesResponseType<VersionResponse>(StatusCodes.Status200OK)]
        public OkResult Answers([FromBody] ProbeHttpRequest request) => Ok();

        [HttpPut("silent")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public NoContentResult Silent([FromBody] ProbeHttpRequest request) => NoContent();

        [HttpPost("post-silent")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public NoContentResult PostSilent([FromBody] ProbeHttpRequest request) => NoContent();

        [HttpPut("without-version")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public NoContentResult WithoutVersion([FromBody] string request) => NoContent();
    }
}
