using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// B7 de las acciones (ADR 0068 §3, §5 y §9): la fuente de Roles resuelve, valida y vuelve a resolver en el alcance de solo
/// lectura, con quien pide. Cada escenario corre sus cinco casos y el válido se guarda por la ruta de siempre con lo que
/// muestra la propuesta. Quitar un permiso que sigue incluido por otro que queda vuelve al modelo con su ruta (260), y quien
/// no tiene un permiso no lo puede dar en un rol: ni se lista, ni se resuelve, ni se vuelve a mostrar.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class RoleActionSourceTests(ApiFactory factory)
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-AR");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("roles.create@1")]
    [InlineData("roles.edit@1")]
    [InlineData("roles.remove@1")]
    public async Task Each_case_of_the_scenario_resolves_or_returns_to_the_model_and_the_valid_one_saves_by_its_route(string action)
    {
        var scenario = await new RoleActionScenarios().PrepareAsync(factory, action, Ct);
        using var client = scenario.Client;
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        AssistantActionReview? valid = null;
        foreach (var (name, @case) in scenario.Cases)
        {
            var proposed = await RunAsync(actor, action, (source, definition, ct) => ProposeAsync(source, definition, @case, ct));
            if (@case.RejectedPath is { } path)
            {
                Assert.True(proposed.IsFailure, name);
                var rejected = Assert.IsType<ValidationError>(proposed.Error);
                Assert.True(rejected.Errors.ContainsKey(path), $"{name}: {string.Join(", ", rejected.Errors.Keys)}");
                continue;
            }
            Assert.True(proposed.IsSuccess, $"{name}: {(proposed.IsFailure ? Describe(proposed.Error) : string.Empty)}");
            var resolution = proposed.Value;
            Assert.Equal(action != "roles.create@1", resolution.TargetId is not null);
            Assert.Empty(resolution.SubjectUserIds);
            Assert.Equal(@case.Doubtful.Order(StringComparer.Ordinal),
                resolution.Values.Where(value => value.Doubt is not null).Select(value => value.Field).Order(StringComparer.Ordinal));
            var reviewed = await RunAsync(actor, action, (source, _, ct) => source.ReviewAsync(resolution, ct));
            Assert.True(reviewed.IsSuccess, $"{name}: {(reviewed.IsFailure ? reviewed.Error.Code : string.Empty)}");
            // Cada opción que muestra la pantalla tiene su nombre, leído del servidor; nunca el texto del modelo.
            Assert.All(reviewed.Value.Values.Where(value => value.Field is not (RoleActionSource.NameField or RoleActionSource.DescriptionField)),
                value => Assert.False(string.IsNullOrEmpty(value.Label), $"{name}: {value.Field}"));
            if (@case.ShownLabel is { } label)
                Assert.Contains(label, reviewed.Value.Values.Select(value => value.Label).Append(reviewed.Value.Target?.Label));
            if (name == "valid") valid = reviewed.Value;
        }

        Assert.NotNull(valid);
        using var saved = await scenario.SaveAsync(valid, Ct);
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync(Ct));
        if (action == "roles.create@1")
        {
            Assert.Equal(new[] { "SpecificCompany", "Una empresa específica" }, valid.Values
                .Where(value => value.Field == RoleActionSource.ScopeField).SelectMany(value => new[] { value.Value.GetString(), value.Label }));
            Assert.Contains(valid.Values, value => value.Field == RoleActionSource.CompanyField && value.Label == "Delta");
        }
        if (action == "roles.edit@1")
        {
            using var detail = await client.GetFromJsonAsync<JsonDocument>($"/api/roles/{valid.Target!.Id:D}", Ct);
            Assert.Equal(new[] { Permissions.Settings.Read },
                detail!.RootElement.GetProperty("permissions").EnumerateArray().Select(code => code.GetString()));
        }
        if (action == "roles.remove@1")
        {
            // Dado de baja, la propuesta ya no resuelve su destino: se ve «ya no disponible».
            var gone = await RunAsync(actor, action, (source, definition, ct) =>
                source.ReviewAsync(new AssistantActionResolution(definition, valid.Target!.Id, valid.Target.Version, []), ct));
            Assert.Equal(ErrorType.NotFound, gone.Error.Type);
        }
    }

    [Fact]
    public async Task Removing_a_permission_still_included_by_one_that_stays_returns_to_the_model_with_its_path()
    {
        var scenario = await new RoleActionScenarios().PrepareAsync(factory, "roles.edit@1", Ct);
        using var owner = scenario.Client;
        // Administrar usuarios se guarda con Ver usuarios, que incluye (260).
        await OrganizationUserActionScenarios.CreateRoleAsync(owner, "Supervisores", Permissions.Users.Manage, null, Ct);
        var actor = await AssistantExecutionActorTests.CaptureAsync(owner);

        var included = RoleActionScenarios.Case("Supervisores", (RoleActionSource.PermissionsField,
            new object[] { new { op = "add", value = "Ver la auditoría" }, new { op = "remove", value = "Ver usuarios" } }));
        var rejected = await RunAsync(actor, "roles.edit@1", (source, definition, ct) => ProposeAsync(source, definition, included, ct));
        Assert.Equal(new[] { RoleActionSource.PermissionIncludedCode },
            Assert.IsType<ValidationError>(rejected.Error).Errors["permissions[1]"]);

        var both = RoleActionScenarios.Case("Supervisores", (RoleActionSource.PermissionsField,
            new object[] { new { op = "remove", value = "Administrar usuarios" }, new { op = "remove", value = "Ver usuarios" }, "Ver la auditoría" }));
        var accepted = await RunAsync(actor, "roles.edit@1", (source, definition, ct) => ProposeAsync(source, definition, both, ct));
        Assert.True(accepted.IsSuccess, accepted.IsFailure ? Describe(accepted.Error) : null);
    }

    [Fact]
    public async Task Who_lacks_a_permission_cannot_list_resolve_or_see_again_a_role_that_gives_it()
    {
        var scenario = await new RoleActionScenarios().PrepareAsync(factory, "roles.create@1", Ct);
        using var owner = scenario.Client;
        var manager = await OrganizationUserActionScenarios.CreateRoleAsync(owner, "Administra roles", Permissions.Roles.Manage, null, Ct);
        var settings = await OrganizationUserActionScenarios.CreateRoleAsync(owner, "Mira la configuración", Permissions.Settings.Read, null, Ct);
        // Ana administra roles y ve la configuración, pero no la auditoría.
        using var anaClient = AssistantTestData.Client(factory);
        var ana = await AccountJourney.RegisterAsync(factory, anaClient, Ct);
        var anaMember = await OrganizationJourney.AddActiveMemberAsync(factory, scenario.TenantId, ana.UserId, Ct);
        await OrganizationUserActionScenarios.AssignAsync(owner, anaMember, [manager, settings], Ct);
        await AccountJourney.AuthorizeAsync(anaClient, "business", Ct, scenario.TenantId);
        var anaActor = await AssistantExecutionActorTests.CaptureAsync(anaClient);
        var ownerActor = await AssistantExecutionActorTests.CaptureAsync(owner);

        var listed = await RunAsync(anaActor, "roles.create@1", (source, definition, ct) => source.ListChoicesAsync(
            new AssistantActionChoiceRequest(definition, RoleActionSource.PermissionsField, "ver", 1, RoleActionScenarios.Organization), ct));
        Assert.Equal(new[] { "Ver la configuración", "Ver roles" }, listed.Value.Items.Select(row => row.Name));

        var notHeld = RoleActionScenarios.Case(null, (RoleActionSource.NameField, "Auditores"),
            (RoleActionSource.ScopeField, RoleActionScenarios.Organization), (RoleActionSource.PermissionsField, new[] { "Ver la auditoría" }));
        var rejected = await RunAsync(anaActor, "roles.create@1", (source, definition, ct) => ProposeAsync(source, definition, notHeld, ct));
        Assert.Equal(new[] { RoleErrors.PermissionsNotHeldCode }, Assert.IsType<ValidationError>(rejected.Error).Errors["permissions[0]"]);

        var held = RoleActionScenarios.Case(null, (RoleActionSource.NameField, "Auditores"),
            (RoleActionSource.ScopeField, RoleActionScenarios.Organization), (RoleActionSource.PermissionsField, new[] { "Ver la configuración" }));
        var accepted = await RunAsync(anaActor, "roles.create@1", (source, definition, ct) => ProposeAsync(source, definition, held, ct));
        Assert.True(accepted.IsSuccess, accepted.IsFailure ? Describe(accepted.Error) : null);

        // La propuesta que preparó el Dueño, abierta por Ana: el permiso que no puede dar la deja «ya no disponible» por el permiso.
        var byOwner = await RunAsync(ownerActor, "roles.create@1", (source, definition, ct) => ProposeAsync(source, definition, notHeld, ct));
        var reviewed = await RunAsync(anaActor, "roles.create@1", (source, _, ct) => source.ReviewAsync(byOwner.Value, ct));
        Assert.Equal(ErrorType.Forbidden, reviewed.Error.Type);
    }

    private static async Task<Result<AssistantActionResolution>> ProposeAsync(IAssistantActionSource source, AssistantActionDefinition definition,
        AssistantActionScenarioCase @case, CancellationToken ct)
    {
        var resolved = await source.ResolveAsync(new AssistantActionDraft(definition, @case.Target, @case.Values), ct);
        if (resolved.IsFailure) return resolved;
        var validated = await source.ValidateAsync(resolved.Value, ct);
        return validated.IsSuccess ? resolved : validated.Error;
    }

    // Como en el turno: con el actor revalidado del worker, en el alcance de solo lectura y en el idioma de quien pide.
    private async Task<T> RunAsync<T>(ExecutionActor actor, string action,
        Func<IAssistantActionSource, AssistantActionDefinition, CancellationToken, Task<T>> work)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = Spanish;
        try
        {
            using var scope = factory.Services.CreateScope();
            var services = scope.ServiceProvider;
            var prepared = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
            Assert.True(prepared.IsSuccess, prepared.IsFailure ? prepared.Error.Code : null);
            using (services.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value))
                return await services.GetRequiredService<IAssistantReadOnlyScope>().RunAsync<IEnumerable<IAssistantActionSource>, T>((sources, ct) =>
                {
                    var source = sources.Single(item => item.Definitions.Any(definition => definition.Key == action));
                    return work(source, source.Definitions.Single(definition => definition.Key == action), ct);
                }, Ct);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    private static string Describe(Error error) => error is ValidationError validation
        ? string.Join("; ", validation.Errors.Select(field => $"{field.Key}: {string.Join(",", field.Value)}"))
        : error.Code;
}
