using System.Reflection;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>Las colecciones JSON de recursos que crecen deben paginar; sólo los catálogos son listas completas.</summary>
public sealed class GrowingCollectionContractTests
{
    // Relaciones del recurso leído y catálogos cerrados. Cada permiso es por propiedad: nunca por controller o área.
    private static readonly HashSet<string> NestedRelations = new(StringComparer.Ordinal)
    {
        "EffectivePermissions.organization", "EffectivePermissions.platform", "MeResponse.permissions",
        "LoginChannelAvailability.countries", "PlatformAccountDetailResponse.operatorPermissions",
        "PlatformAccountFilterCounts.kinds", "PlatformAccountFilterCounts.states", "PlatformRecoveryFilterCounts.statuses",
        "PlatformTenantFilterCounts.sources", "PlatformTenantFilterCounts.statuses",
        "AccountLoginMethodsResponse.methods", "AccountLoginMethodsResponse.soleOwnerOrganizations",
        "LoginMethodsResponse.channels", "MeResponse.organizations", "MeResponse.features", "MeResponse.moduleCapabilities", "MeResponse.pendingLegalDocuments",
        "ModuleCapabilityResponse.capabilities", "ModuleCapabilityResponse.features",
        "HomeResponse.companies", "HomeResponse.pendingGroups", "HomeCompanyCard.roles",
        "OrganizationUserHttpResponse.roles", "OrganizationUserHttpResponse.companies", "OrganizationUserCompanyRow.roles",
        "CompanyMemberHttpResponse.roles", "CompanyCandidateResponse.companies",
        "RoleResponse.permissions", "PermissionGroupResponse.permissions",
        "PlatformAccountDetailResponse.loginMethods", "PlatformAccountDetailResponse.organizations", "PlatformAccountDetailResponse.soleOwnerOrganizations",
        "PlatformTenantDetailResponse.owners", "PlatformTenantFeaturesResponse.features",
        "PlatformLegalDocumentGroupResponse.versions", "PlatformLegalDocumentResponse.contents",
        "OrganizationAuditEntryResponse.changes", "PlatformAuditEventResponse.changes",
        "AssistantStateResponse.models", "AssistantStateResponse.quickQueries", "AssistantModelTechnicalResponse.efforts",
        "OrganizationAssistantSettingsResponse.platformKeyModels", "OrganizationAssistantSettingsResponse.ownKeyModels",
        "PlatformAssistantSettingsResponse.allowedModelIds", "AssistantMessageResponse.attachments", "AssistantMessageResponse.links",
        "CurrencyReferenceHttpResponse.cultures", "CountryReferenceHttpResponse.cultures", "TimeZoneReferenceHttpResponse.countryCodes",
        "CultureReferenceHttpResponse.cultures", "TaxIdTypeReferenceHttpResponse.cultures",
        "ReferenceDataHttpResponse.currencies", "ReferenceDataHttpResponse.countries", "ReferenceDataHttpResponse.timeZones",
        "ReferenceDataHttpResponse.cultures", "ReferenceDataHttpResponse.taxIdTypes",
    };

    [Fact]
    public void Nested_JSON_collections_require_a_bound_or_an_explicit_resource_relationship()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs", "contracts", "openapi.json")));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var offenders = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            if (!path.Value.TryGetProperty("get", out var get)) continue;
            foreach (var response in get.GetProperty("responses").EnumerateObject().Where(response => response.Name.StartsWith('2')))
            {
                if (!response.Value.TryGetProperty("content", out var content) || !content.TryGetProperty("application/json", out var json)) continue;
                InspectNested(json.GetProperty("schema"), schemas, new HashSet<string>(StringComparer.Ordinal), offenders);
            }
        }
        Assert.Empty(offenders.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Nested_detector_rejects_a_pending_list_even_inside_a_paged_envelope()
    {
        using var schemas = JsonDocument.Parse("""{"PagedResultOfHomeResponse":{"properties":{"items":{"type":"array","items":{"$ref":"#/components/schemas/HomeResponse"}}}},"HomeResponse":{"properties":{"pending":{"type":"array","items":{"type":"string"}}}}}""");
        using var response = JsonDocument.Parse("""{"$ref":"#/components/schemas/PagedResultOfHomeResponse"}""");
        var offenders = new HashSet<string>(StringComparer.Ordinal);
        InspectNested(response.RootElement, schemas.RootElement, [], offenders);
        Assert.Contains("HomeResponse.pending", offenders);
        Assert.DoesNotContain("HomeResponse.pending", NestedRelations);
    }

    private static void InspectNested(JsonElement schema, JsonElement schemas, HashSet<string> visited, HashSet<string> offenders)
    {
        if (schema.TryGetProperty("$ref", out var reference))
        {
            var name = reference.GetString()!.Split('/')[^1];
            if (!visited.Add(name)) return;
            var definition = schemas.GetProperty(name);
            if (definition.TryGetProperty("properties", out var properties))
                foreach (var property in properties.EnumerateObject())
                {
                    var key = $"{name}.{property.Name}";
                    var pagedItems = property.Name == "items" && (name.StartsWith("PagedResultOf", StringComparison.Ordinal) || name.StartsWith("CursorResultOf", StringComparison.Ordinal));
                    foreach (var variant in Variants(property.Value))
                        if (variant.TryGetProperty("items", out _) && !variant.TryGetProperty("maxItems", out _) && !pagedItems && !NestedRelations.Contains(key)) offenders.Add(key);
                    InspectNested(property.Value, schemas, visited, offenders);
                }
            return;
        }
        foreach (var variant in Variants(schema))
        {
            if (variant.TryGetProperty("$ref", out _)) InspectNested(variant, schemas, visited, offenders);
            if (variant.TryGetProperty("items", out var items)) InspectNested(items, schemas, visited, offenders);
            if (variant.TryGetProperty("additionalProperties", out var values) && values.ValueKind == JsonValueKind.Object) InspectNested(values, schemas, visited, offenders);
        }
    }

    private static IEnumerable<JsonElement> Variants(JsonElement schema)
    {
        yield return schema;
        foreach (var composition in new[] { "anyOf", "oneOf", "allOf" })
            if (schema.TryGetProperty(composition, out var variants))
                foreach (var variant in variants.EnumerateArray())
                    foreach (var nested in Variants(variant)) yield return nested;
    }
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
        var actions = ApiAssembly.GetTypes().Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
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
    public void Inherited_collection_routes_use_the_concrete_controller_prefix()
    {
        var action = typeof(InheritedCollectionProbe).GetMethod(nameof(CollectionProbeBase.List))!;
        Assert.Equal("/probe/items", Route(action));
        Assert.Equal("InheritedCollectionProbe.List", Key(action));
    }

    [Fact]
    public void Legacy_collection_limits_and_paged_successors_are_published_in_OpenAPI()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(SolutionRoot.FullPath, "docs", "contracts", "openapi.json")));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var action in ApiAssembly.GetTypes().Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
                     .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance)))
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

    private static string Key(MethodInfo method) => $"{method.ReflectedType!.Name}.{method.Name}";
    private static string Route(MethodInfo method) => "/" + string.Join("/", new[]
    {
        method.ReflectedType!.GetCustomAttribute<RouteAttribute>()?.Template.Trim('/'), method.GetCustomAttribute<HttpGetAttribute>()?.Template?.Trim('/'),
    }.Where(part => !string.IsNullOrEmpty(part)));

    private abstract class CollectionProbeBase : ControllerBase
    {
        [HttpGet("items")]
        [ProducesResponseType<IReadOnlyList<Guid>>(200)]
        public OkResult List() => Ok();
    }

    [Route("probe")]
    private sealed class InheritedCollectionProbe : CollectionProbeBase;
}
