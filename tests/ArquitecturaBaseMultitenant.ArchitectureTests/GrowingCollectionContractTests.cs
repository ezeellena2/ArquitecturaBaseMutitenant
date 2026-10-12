using System.Reflection;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>Las colecciones JSON de recursos que crecen deben paginar; sólo los catálogos son listas completas.</summary>
public sealed class GrowingCollectionContractTests
{
    private static readonly Assembly ApiAssembly = Assembly.Load("ArquitecturaBaseMultitenant.Api");
    private static readonly HashSet<string> Catalogs = new(StringComparer.Ordinal)
    {
        "ReferenceDataController.GetCurrencies", "ReferenceDataController.GetCountries", "ReferenceDataController.GetTimeZones",
        "ReferenceDataController.GetCultures", "ReferenceDataController.GetTaxIdTypes", "PermissionsController.List",
        // Los dos grupos Terms/Privacy son el catálogo cerrado de tipos de documentos, no una lista de recursos.
        "PlatformLegalDocumentsController.List",
        // Features.All es el catálogo de módulos instalados, no filas que crecen con organizaciones o usuarios.
        "PlatformFeaturesController.List",
    };
    private static readonly Type[] Collections = [typeof(IReadOnlyList<>), typeof(IReadOnlyCollection<>), typeof(IEnumerable<>),
        typeof(ICollection<>), typeof(IList<>), typeof(List<>)];

    [Fact]
    public void Growing_JSON_collections_are_paged_or_have_a_verifiable_bounded_legacy_successor()
    {
        var actions = ApiAssembly.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(method => method.IsDefined(typeof(HttpGetAttribute))).ToArray();
        var offenders = actions.Where(method => !Catalogs.Contains(Key(method)))
            .SelectMany(method => method.GetCustomAttributes<ProducesResponseTypeAttribute>()
                .Where(response => response.StatusCode is >= 200 and < 300 && CollectionElement(response.Type) is not null)
                .Where(response => !HasPagedSuccessor(method, response.Type, actions))
                .Select(response => $"{Key(method)}: {response.Type.Name}"))
            .Order(StringComparer.Ordinal).ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Detection_covers_arrays_and_list_interfaces_but_not_binary_or_paged_responses()
    {
        Assert.Equal(typeof(Guid), CollectionElement(typeof(Guid[])));
        Assert.Equal(typeof(Guid), CollectionElement(typeof(IReadOnlyList<Guid>)));
        Assert.Equal(typeof(Guid), CollectionElement(typeof(IEnumerable<Guid>)));
        Assert.Null(CollectionElement(typeof(byte[]))); // System.Text.Json representa byte[] como base64, no array JSON.
        Assert.Null(CollectionElement(typeof(PagedResult<Guid>)));
        Assert.Null(CollectionElement(typeof(CursorResult<Guid>)));
        Assert.DoesNotContain(Catalogs, entry => entry.Contains("Domains", StringComparison.Ordinal) || entry.Contains("Audit", StringComparison.Ordinal));
    }

    [Fact]
    public void Legacy_collection_limits_and_paged_successors_are_published_in_OpenAPI()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs", "contracts", "openapi.json")));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var action in ApiAssembly.GetTypes().Where(type => typeof(ControllerBase).IsAssignableFrom(type))
                     .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)))
        {
            if (action.GetCustomAttribute<BoundedLegacyCollectionAttribute>() is not { } legacy) continue;
            // OpenAPI normaliza las restricciones de ASP.NET de parámetros de ruta.
            var path = Route(action).Replace(":guid", "", StringComparison.Ordinal);
            var operation = paths.GetProperty(path).GetProperty("get");
            Assert.True(operation.GetProperty("deprecated").GetBoolean());
            Assert.Equal(legacy.Successor.Replace(":guid", "", StringComparison.Ordinal), operation.GetProperty("x-successor").GetString());
            Assert.Equal(legacy.MaxItems, operation.GetProperty("responses").GetProperty("200").GetProperty("content")
                .GetProperty("application/json").GetProperty("schema").GetProperty("maxItems").GetInt32());
            Assert.True(paths.GetProperty(legacy.Successor.Replace(":guid", "", StringComparison.Ordinal)).TryGetProperty("get", out _));
        }
    }

    private static bool HasPagedSuccessor(MethodInfo action, Type collection, MethodInfo[] actions)
    {
        var legacy = action.GetCustomAttribute<BoundedLegacyCollectionAttribute>();
        if (legacy is null || legacy.MaxItems != PagedRequest.DefaultPageSize) return false;
        return actions.Any(successor => Route(successor).Replace(":guid", "", StringComparison.Ordinal)
            == legacy.Successor.Replace(":guid", "", StringComparison.Ordinal)
            && successor.GetCustomAttributes<ProducesResponseTypeAttribute>().Any(response =>
                response.StatusCode == 200 && response.Type.IsGenericType && response.Type.GetGenericTypeDefinition() == typeof(PagedResult<>)
                && response.Type.GenericTypeArguments[0] == CollectionElement(collection)));
    }

    private static Type? CollectionElement(Type type) => type == typeof(byte[]) ? null
        : type.IsArray ? type.GetElementType()
        : type.IsGenericType && Collections.Contains(type.GetGenericTypeDefinition()) ? type.GenericTypeArguments[0] : null;

    private static string Key(MethodInfo method) => $"{method.DeclaringType!.Name}.{method.Name}";
    private static string Route(MethodInfo method) => "/" + string.Join("/", new[]
    {
        method.DeclaringType!.GetCustomAttribute<RouteAttribute>()?.Template.Trim('/'), method.GetCustomAttribute<HttpGetAttribute>()?.Template?.Trim('/'),
    }.Where(part => !string.IsNullOrEmpty(part)));
}
