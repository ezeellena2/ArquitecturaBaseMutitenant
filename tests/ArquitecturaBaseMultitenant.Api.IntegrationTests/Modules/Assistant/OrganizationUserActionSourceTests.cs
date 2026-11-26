using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// B6 de las acciones (ADR 0068 §3, §5 y §9): la fuente de Usuarios resuelve, valida y vuelve a resolver en el alcance de
/// solo lectura, con quien pide. Cada escenario corre sus cinco casos (válido, inválido, ambiguo, con instrucciones escondidas
/// y con algo de otra organización), y el válido se guarda por la ruta de siempre con lo que muestra la propuesta. Quien no
/// tiene un permiso no puede dar el rol que lo trae: ni se lista, ni se resuelve, ni se vuelve a mostrar.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class OrganizationUserActionSourceTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("users.invite@1")]
    [InlineData("users.edit@1")]
    [InlineData("users.remove@1")]
    public async Task Each_case_of_the_scenario_resolves_or_returns_to_the_model_and_the_valid_one_saves_by_its_route(string action)
    {
        var scenario = await new OrganizationUserActionScenarios().PrepareAsync(factory, action, Ct);
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
            Assert.Equal(KindOf(action) != AssistantActionKind.Create, resolution.TargetId is not null);
            Assert.Equal(KindOf(action) == AssistantActionKind.Create ? 0 : 1, resolution.SubjectUserIds.Count);
            Assert.Equal(@case.Doubtful.Order(StringComparer.Ordinal),
                resolution.Values.Where(value => value.Doubt is not null).Select(value => value.Field).Order(StringComparer.Ordinal));
            var reviewed = await RunAsync(actor, action, (source, _, ct) => source.ReviewAsync(resolution, ct));
            Assert.True(reviewed.IsSuccess, $"{name}: {(reviewed.IsFailure ? reviewed.Error.Code : string.Empty)}");
            // Todo lo que muestra la pantalla tiene su nombre, leído del servidor; nunca el texto del modelo.
            Assert.All(reviewed.Value.Values.Where(value => value.Field != OrganizationUserActionSource.EmailField),
                value => Assert.False(string.IsNullOrEmpty(value.Label), $"{name}: {value.Field}"));
            if (@case.ShownLabel is { } label)
                Assert.Contains(label, reviewed.Value.Values.Select(value => value.Label).Append(reviewed.Value.Target?.Label));
            if (name == "valid") valid = reviewed.Value;
        }

        using var saved = await scenario.SaveAsync(client, valid!, null, Ct);
        Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync(Ct));
        if (action == "users.edit@1")
        {
            using var detail = await client.GetFromJsonAsync<JsonDocument>($"/api/users/{valid!.Target!.Id:D}", Ct);
            Assert.Equal(new[] { "Ventas mayoristas" }, detail!.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetProperty("name").GetString()));
        }
        if (action == "users.remove@1")
        {
            // Quitada, la propuesta ya no resuelve su destino: se ve «ya no disponible».
            var gone = await RunAsync(actor, action, (source, definition, ct) =>
                source.ReviewAsync(new AssistantActionResolution(definition, valid!.Target!.Id, valid.Target.Version, []), ct));
            Assert.Equal(ErrorType.NotFound, gone.Error.Type);
        }
    }

    [Fact]
    public async Task Who_lacks_a_permission_cannot_list_resolve_or_see_again_the_role_that_brings_it()
    {
        var scenario = await new OrganizationUserActionScenarios().PrepareAsync(factory, "users.invite@1", Ct);
        using var owner = scenario.Client;
        var inviter = await OrganizationUserActionScenarios.CreateRoleAsync(owner, "Invitadora", Permissions.Users.Invite, null, Ct);
        // Invitadora trae users.invite; Ana la tiene junto a Ventas mayoristas (settings.read), pero no Ventas (audit.read).
        using var anaClient = AssistantTestData.Client(factory);
        var ana = await AccountJourney.RegisterAsync(factory, anaClient, Ct);
        var anaMember = await OrganizationJourney.AddActiveMemberAsync(factory, scenario.TenantId, ana.UserId, Ct);
        var ownerActor = await AssistantExecutionActorTests.CaptureAsync(owner);
        var roleIds = await RunAsync(ownerActor, "users.invite@1", async (source, definition, ct) =>
            (await source.ListChoicesAsync(new AssistantActionChoiceRequest(definition, OrganizationUserActionSource.RolesField, null, 1, null), ct))
                .Value.Items.ToDictionary(row => row.Name, row => Guid.Parse(row.Id)));
        await OrganizationUserActionScenarios.AssignAsync(owner, anaMember, [inviter, roleIds["Ventas mayoristas"]], Ct);
        await AccountJourney.AuthorizeAsync(anaClient, "business", Ct, scenario.TenantId);
        var anaActor = await AssistantExecutionActorTests.CaptureAsync(anaClient);

        var listed = await RunAsync(anaActor, "users.invite@1", (source, definition, ct) =>
            source.ListChoicesAsync(new AssistantActionChoiceRequest(definition, OrganizationUserActionSource.RolesField, "ventas", 1, null), ct));
        Assert.Equal(new[] { "Ventas mayoristas" }, listed.Value.Items.Select(row => row.Name));

        var notHeld = new AssistantActionScenarioCase(null, [Input(OrganizationUserActionSource.EmailField, OrganizationUserActionScenarios.Email),
            Input(OrganizationUserActionSource.RolesField, new[] { "Ventas" })]);
        var rejected = await RunAsync(anaActor, "users.invite@1", (source, definition, ct) => ProposeAsync(source, definition, notHeld, ct));
        Assert.Equal(new[] { MemberErrors.RolesNotHeldCode }, Assert.IsType<ValidationError>(rejected.Error).Errors["roles[0]"]);

        var held = notHeld with { Values = [notHeld.Values[0], Input(OrganizationUserActionSource.RolesField, new[] { "Ventas mayoristas" })] };
        var accepted = await RunAsync(anaActor, "users.invite@1", (source, definition, ct) => ProposeAsync(source, definition, held, ct));
        Assert.True(accepted.IsSuccess, accepted.IsFailure ? Describe(accepted.Error) : null);

        // La propuesta que preparó el Dueño, abierta por Ana: el rol que no puede dar la deja «ya no disponible» por el permiso.
        var byOwner = await RunAsync(ownerActor, "users.invite@1", (source, definition, ct) => ProposeAsync(source, definition, notHeld, ct));
        var reviewed = await RunAsync(anaActor, "users.invite@1", (source, _, ct) => source.ReviewAsync(byOwner.Value, ct));
        Assert.Equal(ErrorType.Forbidden, reviewed.Error.Type);
    }

    private static AssistantActionKind KindOf(string action) =>
        OrganizationUserActionSource.Actions.Single(definition => definition.Key == action).Kind;

    private static AssistantActionInput Input(string field, object value) => new(field, JsonSerializer.SerializeToElement(value));

    private static async Task<Result<AssistantActionResolution>> ProposeAsync(IAssistantActionSource source, AssistantActionDefinition definition,
        AssistantActionScenarioCase @case, CancellationToken ct)
    {
        var resolved = await source.ResolveAsync(new AssistantActionDraft(definition, @case.Target, @case.Values), ct);
        if (resolved.IsFailure) return resolved;
        var validated = await source.ValidateAsync(resolved.Value, ct);
        return validated.IsSuccess ? resolved : validated.Error;
    }

    // Como en el turno: con el actor revalidado del worker y en el alcance de solo lectura.
    private async Task<T> RunAsync<T>(ExecutionActor actor, string action,
        Func<IAssistantActionSource, AssistantActionDefinition, CancellationToken, Task<T>> work)
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

    private static string Describe(Error error) => error is ValidationError validation
        ? string.Join("; ", validation.Errors.Select(field => $"{field.Key}: {string.Join(",", field.Value)}"))
        : error.Code;
}
