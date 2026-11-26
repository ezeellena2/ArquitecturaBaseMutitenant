using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Roles;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Users;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Las acciones declaradas para el asistente (ADR 0068, «Verificación»), en todas las fuentes de Application, del núcleo y
/// de los módulos: el id con versión y la forma; la ruta que guarda, que existe, escribe y tiene el requisito de sus
/// atributos; los campos de la lista cerrada, con sus límites y sin nombres prohibidos; los textos en es y en; las rutas que
/// no se declaran, y el árbol de dependencias de cada fuente, que solo lee. La traducción de cada pantalla al módulo va en
/// <c>Modules/Assistant</c>, que sale con él. Los casos de cada acción nacen con su escenario de la batería genérica (B8),
/// en el proyecto de integración, y los comprueba ella.
/// </summary>
public sealed partial class AssistantActionDeclarationTests
{
    private const int MaxIdLength = 80;

    private static readonly Assembly ApplicationAssembly = typeof(IAssistantActionSource).Assembly;
    private static readonly Assembly[] ProbeAssemblies = [ApplicationAssembly, typeof(AssistantActionDeclarationTests).Assembly];

    private static readonly Lazy<RouteAction[]> ApiRoutes = new(() => typeof(ControllerResultExtensions).Assembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsSubclassOf(typeof(ControllerBase)))
        .SelectMany(RoutesOf)
        .ToArray());

    // La lista de lo permitido (0068 §5): lo que no se recorre porque solo lee.
    private static readonly Type[] RequestContext = [typeof(ICurrentUser), typeof(ITenantContext), typeof(IRequestInfo), typeof(IPublicSiteContext)];
    private static readonly Type[] PermissionReads = [typeof(IPermissionService), typeof(IPlatformPermissionService)];
    private static readonly Type[] OptionReads = [typeof(IOptions<>), typeof(IOptionsSnapshot<>), typeof(IOptionsMonitor<>)];
    private static readonly string[] CacheReadVerbs = ["Get", "List", "Find", "Read", "Count", "Exists", "Search"];

    // Lo que se recorre: su propio árbol también cumple la lista. Un lector, además, solo devuelve datos.
    private static readonly string[] WalkedSuffixes = ["Reader", "Validator", "Policy", "Resolver"];

    private static readonly AssistantActionFieldType[] LengthTypes =
        [AssistantActionFieldType.Text, AssistantActionFieldType.LongText, AssistantActionFieldType.Email, AssistantActionFieldType.Phone, AssistantActionFieldType.TaxId];
    private static readonly AssistantActionFieldType[] RequiredLengthTypes =
        [AssistantActionFieldType.Text, AssistantActionFieldType.LongText, AssistantActionFieldType.Email];
    private static readonly AssistantActionFieldType[] NumberTypes =
        [AssistantActionFieldType.WholeNumber, AssistantActionFieldType.DecimalNumber, AssistantActionFieldType.Money];

    // Ningún campo se llama como un tenant, un usuario o un secreto, ni como una clave, una conexión, SQL, una expresión o
    // una URL (0068 §4). Se comparan las palabras del nombre en camelCase.
    private static readonly HashSet<string> ForbiddenFieldWords = new(StringComparer.Ordinal)
    {
        "tenant", "tenants", "organization", "organizations", "user", "users", "owner", "owners", "actor", "actors",
        "secret", "secrets", "password", "passwords", "token", "tokens", "credential", "credentials", "key", "keys",
        "connection", "connections", "sql", "expression", "expressions", "script", "scripts", "url", "urls", "uri", "uris",
        "href", "webhook", "webhooks", "endpoint", "endpoints",
    };

    // Una ruta cuyo cuerpo trae una de estas propiedades recibe una clave: no se declara.
    private static readonly string[] SecretBodyWords = ["apikey", "secret", "password", "token", "credential", "connectionstring", "privatekey"];

    [Fact]
    public void Every_action_has_a_unique_versioned_id_and_a_coherent_shape()
    {
        var actions = DeclaredActions();
        Assert.NotEmpty(actions);

        var duplicated = actions.GroupBy(action => action.Key, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: el id con versión está repetido");

        Assert.Empty(duplicated.Concat(actions.SelectMany(ShapeViolations)));
    }

    [Fact]
    public void Fields_are_of_the_closed_list_with_their_limits_and_allowed_names() =>
        Assert.Empty(DeclaredActions().SelectMany(action => FieldViolations(action.Key, action.Fields, insideItems: false)));

    [Fact]
    public void The_requirement_and_the_module_are_those_of_the_save_route_attributes() =>
        Assert.Empty(DeclaredActions().SelectMany(action => RequirementViolations(action, ApiRoutes.Value)));

    [Fact]
    public void The_save_route_exists_writes_and_is_idempotent_when_it_creates() =>
        Assert.Empty(DeclaredActions().SelectMany(action => SaveRouteViolations(action, ApiRoutes.Value)));

    [Fact]
    public void No_action_saves_through_a_route_that_is_not_declared()
    {
        var notDeclarable = NotDeclarableRoutes();
        var violations = new List<string>();
        foreach (var action in DeclaredActions())
        {
            if (Find(action.SaveRoute, ApiRoutes.Value) is { } route && NotDeclarableReason(route, notDeclarable) is { } reason)
            {
                violations.Add($"{action.Key}: {action.SaveRoute} no se declara como acción ({reason})");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Every_action_has_its_texts_in_spanish_and_english()
    {
        var texts = SourceTypes().Select(source => source.Assembly).Distinct().SelectMany(TextSets).ToArray();

        var missing = DeclaredActions().SelectMany(TextKeys)
            .Where(key => !texts.Any(set => set.Spanish.Contains(key) && set.English.Contains(key)));

        Assert.Empty(missing);
    }

    [Fact]
    public void The_dependency_tree_of_every_source_only_reads()
    {
        var sources = SourceTypes();
        Assert.NotEmpty(sources);

        Assert.Empty(sources.SelectMany(source => DependencyViolations(source, [ApplicationAssembly])));
    }

    [Fact]
    public void Detector_accepts_the_allowed_list_through_resolvers_policies_validators_and_reader_ports() =>
        Assert.Empty(DependencyViolations(typeof(ReadingProbeSource), ProbeAssemblies));

    [Fact]
    public void Detector_rejects_what_writes_also_through_a_resolver_and_a_reader_port()
    {
        var violations = DependencyViolations(typeof(WritingProbeSource), ProbeAssemblies);

        Assert.Equal(7, violations.Count);
        Assert.Contains(violations, violation => violation.StartsWith("WritingProbeSource → WritingProbeResolver → IRoleRepository:", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.StartsWith("WritingProbeSource → IWritingProbeReader → WritingProbeReader → IUnitOfWork:", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.StartsWith("WritingProbeSource → ILeakyProbeReader: un lector solo devuelve datos", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.StartsWith("WritingProbeSource → IServiceProvider:", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.StartsWith("WritingProbeSource → IRoleService:", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.StartsWith("WritingProbeSource → WritingProbeGuard:", StringComparison.Ordinal));
        Assert.Contains(violations, violation => violation.StartsWith("WritingProbeSource → IUnitOfWork:", StringComparison.Ordinal));
    }

    /// <summary>
    /// Elegir por nombre, leer un elemento de un <c>Choices</c>, aplicar sus operaciones y los topes son del núcleo
    /// (<c>AssistantActionInputs</c> y <c>AssistantActionLimits</c>), y la empresa y los roles por su nombre, de sus
    /// <c>Resolver</c>: una fuente que se copia para un área nueva no se lleva una versión propia que una corrección no alcanza.
    /// </summary>
    [Fact]
    public void Sources_use_the_shared_helpers_and_limits_instead_of_their_own_copies()
    {
        var sources = SourceTypes();
        Assert.NotEmpty(sources);

        Assert.Empty(sources.SelectMany(CopiedHelpers));
    }

    [Fact]
    public void Detector_finds_a_copied_helper_and_a_limit_of_its_own() =>
        Assert.Equal(["CopyingProbeSource.Match", "CopyingProbeSource.ChoicesPageSize"], CopiedHelpers(typeof(CopyingProbeSource)));

    [Fact]
    public void Detector_rejects_forbidden_field_names_and_accepts_the_others()
    {
        Assert.All(new[] { "tenantId", "userId", "apiKey", "connectionString", "webhookUrl", "organizationId", "sqlFilter", "ownerEmail" },
            name => Assert.True(IsForbiddenFieldName(name), name));
        Assert.All(new[] { "email", "companyRoles", "name", "description", "keyboardLayout", "permissions" },
            name => Assert.False(IsForbiddenFieldName(name), name));
    }

    [Fact]
    public void Detector_rejects_a_bad_shape_and_bad_fields()
    {
        var probe = new AssistantActionDefinition
        {
            Id = "Probes.Remove",
            Version = 0,
            Kind = AssistantActionKind.Remove,
            Requirement = AssistantActionRequirement.Company("probes.manage", "company"),
            SaveRoute = "DELETE api/probes/{id}",
            Confirmation = AssistantActionConfirmation.Page,
            Screen = AssistantActionScreen.Roles,
            Fields =
            [
                new() { Name = "title", Type = AssistantActionFieldType.Text },
                new() { Name = "company", Type = AssistantActionFieldType.Choice, MaxLength = 10 },
                new() { Name = "rows", Type = AssistantActionFieldType.Items, ItemFields = [new() { Name = "inner", Type = AssistantActionFieldType.Items }] },
                new() { Name = "roles", Type = AssistantActionFieldType.Choices, DependsOn = "missing", ItemKey = "id" },
                new() { Name = "apiKey", Type = AssistantActionFieldType.Boolean, Minimum = 1 },
            ],
        };

        var shape = ShapeViolations(probe).ToArray();
        Assert.Contains(shape, violation => violation.Contains("el id es", StringComparison.Ordinal));
        Assert.Contains(shape, violation => violation.Contains("la versión", StringComparison.Ordinal));
        Assert.Contains(shape, violation => violation.Contains("RemoveConfirmation", StringComparison.Ordinal));
        Assert.Contains(shape, violation => violation.Contains("(Target)", StringComparison.Ordinal));
        Assert.Contains(shape, violation => violation.Contains("campo de empresa", StringComparison.Ordinal));

        var fields = FieldViolations(probe.Key, probe.Fields, insideItems: false).ToArray();
        Assert.Contains(fields, violation => violation.StartsWith("Probes.Remove@0.title: un texto lleva su largo", StringComparison.Ordinal));
        Assert.Contains(fields, violation => violation.StartsWith("Probes.Remove@0.company: el largo máximo", StringComparison.Ordinal));
        Assert.Contains(fields, violation => violation.StartsWith("Probes.Remove@0.rows: un Items lleva su tope", StringComparison.Ordinal));
        Assert.Contains(fields, violation => violation.StartsWith("Probes.Remove@0.rows: un Items lleva la clave", StringComparison.Ordinal));
        Assert.Contains(fields, violation => violation.StartsWith("Probes.Remove@0.rows.inner: un Items tiene un solo nivel", StringComparison.Ordinal));
        Assert.Contains(fields, violation => violation.StartsWith("Probes.Remove@0.roles: depende de un campo", StringComparison.Ordinal));
        Assert.Contains(fields, violation => violation.StartsWith("Probes.Remove@0.roles: la clave y los campos de fila", StringComparison.Ordinal));
        Assert.Contains(fields, violation => violation.StartsWith("Probes.Remove@0.apiKey: un campo no se llama", StringComparison.Ordinal));
        Assert.Contains(fields, violation => violation.StartsWith("Probes.Remove@0.apiKey: el mínimo y el máximo", StringComparison.Ordinal));
    }

    [Fact]
    public void Detector_rejects_routes_that_are_not_declared_and_accepts_the_others()
    {
        var notDeclarable = NotDeclarableRoutes();
        var probes = RoutesOf(typeof(ProbeController)).ToDictionary(route => route.Template, StringComparer.Ordinal);

        Assert.NotNull(NotDeclarableReason(probes["api/probes/anonymous"], notDeclarable));
        Assert.NotNull(NotDeclarableReason(probes["api/probes/key"], notDeclarable));
        Assert.Null(NotDeclarableReason(probes["api/probes"], notDeclarable));
        foreach (var listed in new[] { "api/legal/accept", "api/me/deletion", "api/me/login-methods" })
        {
            var writes = ApiRoutes.Value.Where(route => route.Verb != "GET" && Under(route.Template, listed)).ToArray();
            Assert.NotEmpty(writes);
            Assert.All(writes, route => Assert.NotNull(NotDeclarableReason(route, notDeclarable)));
        }

        Assert.Null(NotDeclarableReason(Find("POST api/users/invitations", ApiRoutes.Value)!, notDeclarable));
    }

    private static readonly string[] SharedHelperNames = ["Match", "MatchCompanyAsync", "TryReadOp", "Same", "Contains", "Apply", "IdOf"];

    private static string[] CopiedHelpers(Type source) =>
    [
        .. source.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => SharedHelperNames.Contains(method.Name, StringComparer.Ordinal)).Select(method => $"{source.Name}.{method.Name}"),
        .. source.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => field.IsLiteral && field.FieldType == typeof(int)).Select(field => $"{source.Name}.{field.Name}"),
    ];

    private static Type[] SourceTypes() => ApplicationAssembly.GetTypes()
        .Where(type => type is { IsClass: true, IsAbstract: false }
            && typeof(IAssistantActionSource).IsAssignableFrom(type)
            && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
        .ToArray();

    // Las declaraciones son estáticas (0068 §5): se leen sin construir la fuente ni sus dependencias.
    private static AssistantActionDefinition[] DeclaredActions() => SourceTypes()
        .SelectMany(type => ((IAssistantActionSource)RuntimeHelpers.GetUninitializedObject(type)).Definitions)
        .ToArray();

    private static IEnumerable<string> ShapeViolations(AssistantActionDefinition action)
    {
        var key = action.Key;
        if (!ActionId().IsMatch(action.Id) || action.Id.Length > MaxIdLength)
        {
            yield return $"{key}: el id es área y verbo, en minúsculas y con puntos, de hasta {MaxIdLength}";
        }

        if (action.Version < 1)
        {
            yield return $"{key}: la versión empieza en 1";
        }

        if (!Enum.IsDefined(action.Kind) || !Enum.IsDefined(action.Confirmation) || !Enum.IsDefined(action.Screen) || !Enum.IsDefined(action.Effect))
        {
            yield return $"{key}: la clase, la confirmación, la pantalla y el efecto son de sus listas cerradas";
        }

        if (action.Kind == AssistantActionKind.Remove && action.Confirmation != AssistantActionConfirmation.RemoveConfirmation)
        {
            yield return $"{key}: una acción que elimina se confirma con RemoveConfirmation";
        }

        if (action.Kind is AssistantActionKind.Edit or AssistantActionKind.Remove && action.Target is null)
        {
            yield return $"{key}: editar o eliminar lleva la lectura que encuentra el registro (Target)";
        }

        if (action.FieldsFromTarget && (action.Fields.Count > 0 || action.Target is null))
        {
            yield return $"{key}: los campos que salen del destino van sin campos fijos y con su destino";
        }

        if (action.Module is not null && string.IsNullOrWhiteSpace(action.Module))
        {
            yield return $"{key}: el módulo es su clave, o null en el núcleo";
        }

        var requirement = action.Requirement;
        var complete = requirement.Kind switch
        {
            AssistantActionRequirementKind.Consumer => requirement is { Permission: null, OrganizationPermission: null, CompanyField: null },
            AssistantActionRequirementKind.Organization or AssistantActionRequirementKind.Platform =>
                !string.IsNullOrWhiteSpace(requirement.Permission) && requirement is { OrganizationPermission: null, CompanyField: null },
            AssistantActionRequirementKind.Company => !string.IsNullOrWhiteSpace(requirement.Permission)
                && action.Fields.Any(field => field.Name == requirement.CompanyField && field.Type == AssistantActionFieldType.Record),
            _ => false,
        };
        if (!complete)
        {
            yield return $"{key}: el requisito {requirement.Kind} no está completo; uno de empresa nombra su campo de empresa, un Record";
        }
    }

    private static IEnumerable<string> FieldViolations(string owner, IReadOnlyList<AssistantActionField> fields, bool insideItems)
    {
        foreach (var repeated in fields.GroupBy(field => field.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            yield return $"{owner}.{repeated.Key}: el campo está repetido";
        }

        foreach (var field in fields)
        {
            var at = $"{owner}.{field.Name}";
            if (!FieldName().IsMatch(field.Name))
            {
                yield return $"{at}: el nombre va en camelCase";
            }

            if (IsForbiddenFieldName(field.Name))
            {
                yield return $"{at}: un campo no se llama como un tenant, un usuario, un secreto, una clave, una conexión, SQL, una expresión ni una URL";
            }

            if (!Enum.IsDefined(field.Type))
            {
                yield return $"{at}: el tipo no es de la lista cerrada";
            }

            if (RequiredLengthTypes.Contains(field.Type) && field.MaxLength is not > 0)
            {
                yield return $"{at}: un texto lleva su largo máximo";
            }

            if (field.MaxLength is { } length && (!LengthTypes.Contains(field.Type) || length <= 0))
            {
                yield return $"{at}: el largo máximo es de un texto, y positivo";
            }

            if (field.MaxItems is { } items && (field.Type is not (AssistantActionFieldType.Choices or AssistantActionFieldType.Items) || items <= 0))
            {
                yield return $"{at}: el tope de elementos es de un Choices o un Items, y positivo";
            }

            if (((field.Minimum is not null || field.Maximum is not null) && !NumberTypes.Contains(field.Type)) || field.Minimum > field.Maximum)
            {
                yield return $"{at}: el mínimo y el máximo son de un número, y en orden";
            }

            if (field.DependsOn is { } dependsOn && (dependsOn == field.Name || !fields.Any(other => other.Name == dependsOn)))
            {
                yield return $"{at}: depende de un campo que no está a su lado";
            }

            if (field.Type != AssistantActionFieldType.Items)
            {
                if (field.ItemKey is not null || field.ItemFields is not null)
                {
                    yield return $"{at}: la clave y los campos de fila son de un Items";
                }

                continue;
            }

            if (insideItems)
            {
                yield return $"{at}: un Items tiene un solo nivel";
            }

            if (field.MaxItems is not > 0)
            {
                yield return $"{at}: un Items lleva su tope de filas";
            }

            if (field.ItemKey is null || field.ItemFields?.Any(item => item.Name == field.ItemKey) != true)
            {
                yield return $"{at}: un Items lleva la clave de cada fila, que es uno de sus campos";
            }

            foreach (var violation in FieldViolations(at, field.ItemFields ?? [], insideItems: true))
            {
                yield return violation;
            }
        }
    }

    private static bool IsForbiddenFieldName(string name) =>
        WordBoundary().Split(name).Any(word => ForbiddenFieldWords.Contains(word.ToLowerInvariant()));

    private static IEnumerable<string> RequirementViolations(AssistantActionDefinition action, IReadOnlyList<RouteAction> routes)
    {
        // Si la ruta no existe, lo dice The_save_route_exists_writes_and_is_idempotent_when_it_creates.
        if (Find(action.SaveRoute, routes) is not { } route)
        {
            yield break;
        }

        if (RequirementOf(route) != (action.Requirement with { CompanyField = null }))
        {
            yield return $"{action.Key}: el requisito no es el de {action.SaveRoute} ([Access] y su permiso, con el de organización que también alcanza)";
        }

        var gates = Declared<FeatureGateAttribute>(route).SelectMany(gate => gate.Features).ToHashSet(StringComparer.Ordinal);
        if (action.Module is null ? gates.Count > 0 : !gates.Contains(action.Module))
        {
            yield return $"{action.Key}: el módulo no es el [FeatureGate] de {action.SaveRoute}";
        }
    }

    private static AssistantActionRequirement? RequirementOf(RouteAction route)
    {
        var access = (route.Method.GetCustomAttribute<AccessAttribute>(inherit: true)
            ?? route.Controller.GetCustomAttribute<AccessAttribute>(inherit: true))?.Allowed ?? [];
        return (access, Declared<HasPermissionAttribute>(route), Declared<HasCompanyPermissionAttribute>(route), Declared<HasPlatformPermissionAttribute>(route)) switch
        {
            ([Access.Consumer], [], [], []) => AssistantActionRequirement.Consumer(),
            ([Access.Business], [var granted], [], []) => AssistantActionRequirement.Organization(granted.Permission),
            ([Access.Business], [], [var granted], []) =>
                new(AssistantActionRequirementKind.Company, granted.Permission, granted.OrganizationPermission, null),
            ([Access.Platform], [], [], [var granted]) => AssistantActionRequirement.Platform(granted.Permission),
            _ => null,
        };
    }

    private static T[] Declared<T>(RouteAction route) where T : Attribute =>
        [.. route.Controller.GetCustomAttributes<T>(inherit: true), .. route.Method.GetCustomAttributes<T>(inherit: true)];

    private static IEnumerable<string> SaveRouteViolations(AssistantActionDefinition action, IReadOnlyList<RouteAction> routes)
    {
        if (Find(action.SaveRoute, routes) is not { } route)
        {
            yield return $"{action.Key}: la ruta que guarda ({action.SaveRoute}) no existe, o no es una sola";
            yield break;
        }

        var writes = action.Kind switch
        {
            AssistantActionKind.Create => route.Verb == "POST",
            AssistantActionKind.Edit => route.Verb is "PUT" or "PATCH",
            AssistantActionKind.Remove => route.Verb == "DELETE",
            _ => route.Verb is "POST" or "PUT" or "PATCH" or "DELETE",
        };
        if (!writes)
        {
            yield return $"{action.Key}: la ruta escribe con el verbo de su clase (crear POST, editar PUT o PATCH, eliminar DELETE)";
        }

        if (action.Kind == AssistantActionKind.Create && !route.Method.IsDefined(typeof(IdempotentAttribute), inherit: true))
        {
            yield return $"{action.Key}: una acción que crea guarda por una ruta [Idempotent]";
        }

        if (action.Target is { } target && Find(target.ReadRoute, routes) is not { Verb: "GET" })
        {
            yield return $"{action.Key}: la lectura del destino ({target.ReadRoute}) no existe o no es un GET";
        }
    }

    static partial void ContributeAssistantNotDeclarable(List<NotDeclarableRoute> routes);

    static partial void ContributeWhatsAppNotDeclarable(List<NotDeclarableRoute> routes);

    // Lo que no se declara como acción (0068 §4), además de lo anónimo, lo que pide una prueba reciente del segundo factor y
    // lo que recibe una clave en el cuerpo. Cada módulo suma sus rutas.
    private static List<NotDeclarableRoute> NotDeclarableRoutes()
    {
        List<NotDeclarableRoute> routes =
        [
            new(null, "api/auth", "el ingreso, sus métodos y el segundo factor"),
            new(null, "connect", "el ingreso"),
            new(null, "api/account-recovery", "el ingreso"),
            new(null, "api/me/login-methods", "los métodos de ingreso"),
            new(null, "api/me/external", "los métodos de ingreso"),
            new(null, "api/me/deletion", "la baja de la cuenta"),
            new(null, "api/invitations", "aceptar una invitación"),
            new(null, "api/legal/accept", "aceptar un documento legal"),
            new(null, "api/platform/reauthentication", "una prueba reciente"),
        ];
        ContributeAssistantNotDeclarable(routes);
        ContributeWhatsAppNotDeclarable(routes);
        return routes;
    }

    private static string? NotDeclarableReason(RouteAction route, IReadOnlyList<NotDeclarableRoute> listed)
    {
        if (route.Controller.IsDefined(typeof(AllowAnonymousAttribute), inherit: true)
            || route.Method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true))
        {
            return "es anónima: el ingreso, el registro o una aceptación";
        }

        var descriptor = new ControllerActionDescriptor { ControllerTypeInfo = route.Controller.GetTypeInfo(), MethodInfo = route.Method };
        if (RecentPlatformProof.IsRequired(descriptor, route.Verb))
        {
            return "pide una prueba reciente del segundo factor";
        }

        if (listed.FirstOrDefault(entry => (entry.Verb is null || entry.Verb == route.Verb) && Under(route.Template, entry.Prefix)) is { } match)
        {
            return match.Reason;
        }

        var secret = route.Method.GetParameters()
            .Where(parameter => parameter.IsDefined(typeof(FromBodyAttribute), inherit: false))
            .SelectMany(parameter => parameter.ParameterType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .FirstOrDefault(property => SecretBodyWords.Any(word => property.Name.Contains(word, StringComparison.OrdinalIgnoreCase)));
        return secret is null ? null : $"recibe una clave ({secret.Name})";
    }

    private static IEnumerable<RouteAction> RoutesOf(Type controller)
    {
        var prefixes = controller.GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(route => route.Template)
            .DefaultIfEmpty(string.Empty)
            .ToArray();
        return controller.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public)
            .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
                .SelectMany(http => http.HttpMethods.SelectMany(verb => prefixes.Select(prefix =>
                    new RouteAction(verb, Combine(prefix, http.Template), controller, method)))));
    }

    private static RouteAction? Find(string declared, IReadOnlyList<RouteAction> routes)
    {
        var parts = declared.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        var template = Normalize(parts[1]);
        var matches = routes.Where(route => route.Verb == parts[0] && route.Template == template).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static string Combine(string prefix, string? template) => Normalize(template switch
    {
        null or "" => prefix,
        _ when template.StartsWith('/') || template.StartsWith("~/", StringComparison.Ordinal) => template.TrimStart('~'),
        _ => $"{prefix}/{template}",
    });

    // Sin restricciones ni opcionales en los parámetros, como se escribe la SaveRoute: api/users/{memberId}.
    private static string Normalize(string template) =>
        RouteParameter().Replace(template.Trim('/'), parameter => "{" + parameter.Groups["name"].Value + "}").ToLowerInvariant();

    private static bool Under(string template, string prefix) =>
        template == prefix || template.StartsWith(prefix + "/", StringComparison.Ordinal);

    private static IEnumerable<(HashSet<string> Spanish, HashSet<string> English)> TextSets(Assembly assembly) =>
        assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".resources", StringComparison.Ordinal))
            .Select(name => new ResourceManager(name[..^".resources".Length], assembly))
            .Select(manager => (Keys(manager, CultureInfo.InvariantCulture), Keys(manager, CultureInfo.GetCultureInfo("en"))));

    private static HashSet<string> Keys(ResourceManager manager, CultureInfo culture) =>
        manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false) is { } set
            ? set.Cast<DictionaryEntry>()
                .Where(entry => entry.Value is string value && !string.IsNullOrWhiteSpace(value))
                .Select(entry => (string)entry.Key)
                .ToHashSet(StringComparer.Ordinal)
            : [];

    private static IEnumerable<string> TextKeys(AssistantActionDefinition action) =>
    [
        ActionTexts.NameKey(action),
        ActionTexts.DescriptionKey(action),
        .. action.Fields.SelectMany(field => (field.ItemFields ?? [])
            .Select(item => ActionTexts.FieldKey(action, $"{field.Name}.{item.Name}"))
            .Prepend(ActionTexts.FieldKey(action, field.Name))),
    ];

    private static List<string> DependencyViolations(Type source, IReadOnlyCollection<Assembly> implementations)
    {
        var violations = new List<string>();
        Walk(source, source.Name, implementations, [], violations);
        return violations;
    }

    private static void Walk(Type type, string path, IReadOnlyCollection<Assembly> implementations, HashSet<Type> visited, List<string> violations)
    {
        if (!visited.Add(type))
        {
            return;
        }

        var parameters = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters());
        foreach (var parameter in parameters)
        {
            var dependency = parameter.ParameterType is { IsGenericType: true } enumerable
                && enumerable.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                    ? enumerable.GetGenericArguments()[0]
                    : parameter.ParameterType;
            var at = $"{path} → {Display(dependency)}";
            if (IsReadingLeaf(dependency))
            {
                continue;
            }

            var name = BaseName(dependency);
            if (!WalkedSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal)))
            {
                violations.Add($"{at}: no está en la lista de lo permitido (0068 §5)");
                continue;
            }

            if (name.EndsWith("Reader", StringComparison.Ordinal) && dependency.IsInterface && !ReturnsData(dependency))
            {
                violations.Add($"{at}: un lector solo devuelve datos (0068 §5)");
            }

            if (dependency is { IsInterface: false, IsAbstract: false })
            {
                Walk(dependency, at, implementations, visited, violations);
                continue;
            }

            // Un puerto se recorre por cada implementación de Application, también la de un módulo dueño; la de
            // Infrastructure es el adaptador de persistencia, y la segunda barrera es la transacción de solo lectura.
            foreach (var implementation in implementations.SelectMany(assembly => assembly.GetTypes())
                .Where(candidate => candidate is { IsClass: true, IsAbstract: false } && dependency.IsAssignableFrom(candidate)
                    && !candidate.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)))
            {
                Walk(implementation, $"{at} → {Display(implementation)}", implementations, visited, violations);
            }
        }
    }

    private static bool IsReadingLeaf(Type dependency) =>
        dependency == typeof(TimeProvider)
        || RequestContext.Contains(dependency)
        || PermissionReads.Contains(dependency)
        || (dependency.IsGenericType && OptionReads.Contains(dependency.GetGenericTypeDefinition()))
        || (dependency.IsInterface && dependency.Namespace == typeof(ICurrencyCatalog).Namespace)
        || (dependency.IsInterface && dependency.Namespace == typeof(IRolePermissionCache).Namespace
            && dependency.GetMethods().All(method => CacheReadVerbs.Any(verb => method.Name.StartsWith(verb, StringComparison.Ordinal))));

    private static bool ReturnsData(Type reader) =>
        reader.GetMethods().Concat(reader.GetInterfaces().SelectMany(parent => parent.GetMethods()))
            .All(method => method.ReturnType != typeof(void) && method.ReturnType != typeof(Task) && method.ReturnType != typeof(ValueTask));

    private static string Display(Type type) => type.IsGenericType
        ? $"{BaseName(type)}<{string.Join(", ", type.GetGenericArguments().Select(Display))}>"
        : type.Name;

    private static string BaseName(Type type) => type.Name.Split('`')[0];

    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$", RegexOptions.CultureInvariant)]
    private static partial Regex ActionId();

    [GeneratedRegex(@"^[a-z][a-zA-Z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldName();

    [GeneratedRegex(@"(?<!^)(?=[A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex WordBoundary();

    [GeneratedRegex(@"\{\**(?<name>[A-Za-z0-9_]+)[^}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex RouteParameter();

    private sealed record RouteAction(string Verb, string Template, Type Controller, MethodInfo Method);

    private sealed record NotDeclarableRoute(string? Verb, string Prefix, string Reason);

#pragma warning disable CA1812, CS9113 // Signature-only test probes, inspected by reflection.
    // Una fuente que se copió los ayudantes y un tope en vez de usar los del núcleo.
    private static class CopyingProbeSource
    {
        private const int ChoicesPageSize = 20;

        private static bool Match(string candidate, string text) => candidate.Length > ChoicesPageSize && text.Length > 0;

        internal static bool Probe() => Match("a", "b");
    }

    // Una fuente que solo usa lo permitido, también a través de un Resolver, una Policy, un validador y un puerto lector.
    private sealed class ReadingProbeSource(ICurrentUser user, ITenantContext tenant, TimeProvider clock, IRoleReader roles,
        IValidator<CreateRoleRequest> validator, IPermissionService permissions, ICurrencyCatalog currencies,
        IRolePermissionCache cache, IOptions<ProbeOptions> options, ReadingProbeResolver resolver,
        IEnumerable<ReadingProbePolicy> policies, IReadingProbeReader notes);

    private sealed class ReadingProbeResolver(ICurrentUser user, IRoleReader roles);

    private sealed class ReadingProbePolicy(TimeProvider clock);

    private sealed class ProbeOptions;

    private interface IReadingProbeReader
    {
        Task<string?> FindAsync(Guid id, CancellationToken cancellationToken);
    }

    private sealed class ReadingProbeReader(IRoleReader roles) : IReadingProbeReader
    {
        public Task<string?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<string?>(roles.ToString());
    }

    // Una fuente que escribe: directo, a través de un Resolver con un repositorio y de la implementación de un puerto lector.
    private sealed class WritingProbeSource(WritingProbeResolver resolver, IWritingProbeReader notes, ILeakyProbeReader leaky,
        IServiceProvider services, IRoleService roles, WritingProbeGuard guard, IUnitOfWork unitOfWork);

    private sealed class WritingProbeResolver(IRoleRepository roles);

    private sealed class WritingProbeGuard;

    private interface IWritingProbeReader
    {
        Task<string?> FindAsync(Guid id, CancellationToken cancellationToken);
    }

    private sealed class WritingProbeReader(IUnitOfWork unitOfWork) : IWritingProbeReader
    {
        public Task<string?> FindAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<string?>(unitOfWork.ToString());
    }

    private interface ILeakyProbeReader
    {
        Task ForgetAsync(Guid id, CancellationToken cancellationToken);
    }
#pragma warning restore CA1812, CS9113

    [Route("api/probes")]
    [Access(Access.Business)]
    private sealed class ProbeController : ControllerBase
    {
        [HttpPost]
        public static OkObjectResult Create([FromBody] ProbeRequest request) => new(request);

        [HttpPost("anonymous")]
        [AllowAnonymous]
        public static OkResult Anonymous() => new();

        [HttpPut("key")]
        public static OkObjectResult Key([FromBody] ProbeKeyRequest request) => new(request);
    }

    private sealed record ProbeRequest(string? Name);

    private sealed record ProbeKeyRequest(string? ApiKey);
}
