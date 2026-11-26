using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// B6 de las acciones (ADR 0068 §3, §5 y §9): la fuente de Usuarios resuelve, valida y vuelve a resolver en el alcance de
/// solo lectura, con quien pide. Cada escenario corre sus cinco casos (válido, inválido, ambiguo, con instrucciones escondidas
/// y con algo de otra organización), y el válido se guarda por la ruta de siempre con lo que muestra la propuesta. Quien no
/// tiene un permiso no puede dar el rol que lo trae: ni se lista, ni se resuelve, ni se vuelve a mostrar. Los escenarios
/// corren con un lector de roles que se niega a leer la lista entera: la fuente busca con el lector y lee por id.
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
        using var host = NoWholeRoleListReader.Host(factory);
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        AssistantActionReview? valid = null;
        foreach (var (name, @case) in scenario.Cases)
        {
            var proposed = await RunAsync(actor, action, (source, definition, ct) => ProposeAsync(source, definition, @case, ct), host.Services);
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
            var reviewed = await RunAsync(actor, action, (source, _, ct) => source.ReviewAsync(resolution, ct), host.Services);
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

    /// <summary>
    /// La tarjeta de quitar nombra a la persona y su correo (Asistente-Org-Acciones), así se distingue de otra con el mismo
    /// nombre: el destino lleva el correo verificado que muestra su ficha. Sin correo, o sin nombre (el correo ya es su nombre),
    /// no lleva nada más.
    /// </summary>
    [Fact]
    public async Task The_person_to_remove_carries_the_email_that_tells_her_apart()
    {
        var scenario = await new OrganizationUserActionScenarios().PrepareAsync(factory, "users.remove@1", Ct);
        using var client = scenario.Client;
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        var email = Email.Create("lucia.ferrer." + Guid.NewGuid().ToString("N") + "@example.test").Value;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async work =>
            {
                var user = await services.GetRequiredService<IUserRepository>().CreateAsync("Lucía Ferrer", "es-AR", "America/Argentina/Buenos_Aires", work);
                var method = LoginMethod.CreateEmail(user.Id, email);
                Assert.True(method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                services.GetRequiredService<ApplicationDbContext>().Add(method);
                return Result.Success(user.Id);
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
            await OrganizationJourney.AddActiveMemberAsync(factory, scenario.TenantId, saved.Value, Ct);
        }

        foreach (var (target, detail) in new[] { ("Lucía Ferrer", email.DisplayValue), ("Martín Paz", null) })
        {
            var proposed = await RunAsync(actor, "users.remove@1", (source, definition, ct) =>
                ProposeAsync(source, definition, new AssistantActionScenarioCase(target, []), ct));
            Assert.True(proposed.IsSuccess, $"{target}: {(proposed.IsFailure ? Describe(proposed.Error) : string.Empty)}");
            var reviewed = await RunAsync(actor, "users.remove@1", (source, _, ct) => source.ReviewAsync(proposed.Value, ct));
            Assert.True(reviewed.IsSuccess, reviewed.IsFailure ? reviewed.Error.Code : null);
            Assert.Equal(target, reviewed.Value.Target!.Label);
            Assert.Equal(detail, reviewed.Value.Target.Detail);
        }
    }

    [Fact]
    public async Task Who_lacks_a_permission_cannot_list_resolve_or_see_again_the_role_that_brings_it()
    {
        var scenario = await new OrganizationUserActionScenarios().PrepareAsync(factory, "users.invite@1", Ct);
        using var owner = scenario.Client;
        var inviter = await OrganizationUserActionScenarios.CreateRoleAsync(owner, "Invitadora", Permissions.Users.Invite, null, Ct);
        var reader = await OrganizationUserActionScenarios.CreateRoleAsync(owner, "Lectora de roles", Permissions.Roles.Read, null, Ct);
        // Invitadora trae users.invite y Lectora de roles, roles.read (lo que pide la lista de roles del diálogo); Ana las
        // tiene junto a Ventas mayoristas (settings.read), pero no Ventas (audit.read).
        using var anaClient = AssistantTestData.Client(factory);
        var ana = await AccountJourney.RegisterAsync(factory, anaClient, Ct);
        var anaMember = await OrganizationJourney.AddActiveMemberAsync(factory, scenario.TenantId, ana.UserId, Ct);
        var ownerActor = await AssistantExecutionActorTests.CaptureAsync(owner);
        var roleIds = await RunAsync(ownerActor, "users.invite@1", async (source, definition, ct) =>
            (await source.ListChoicesAsync(new AssistantActionChoiceRequest(definition, OrganizationUserActionSource.RolesField, null, 1, null), ct))
                .Value.Items.ToDictionary(row => row.Name, row => Guid.Parse(row.Id)));
        await OrganizationUserActionScenarios.AssignAsync(owner, anaMember, [inviter, reader, roleIds["Ventas mayoristas"]], Ct);
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

    /// <summary>
    /// Dos personas parecidas: el destino no admite duda y vuelve al modelo con un código del catálogo de miembros, con su
    /// texto en los dos idiomas, y no con la clave de una duda.
    /// </summary>
    [Fact]
    public async Task A_target_like_several_people_returns_to_the_model_with_the_ambiguous_code_of_the_catalog()
    {
        var scenario = await new OrganizationUserActionScenarios().PrepareAsync(factory, "users.remove@1", Ct);
        using var owner = scenario.Client;
        var actor = await AssistantExecutionActorTests.CaptureAsync(owner);

        var rejected = await RunAsync(actor, "users.remove@1", (source, definition, ct) =>
            ProposeAsync(source, definition, new AssistantActionScenarioCase("Martín", []), ct));

        Assert.Equal(new[] { MemberErrors.AmbiguousCode }, Assert.IsType<ValidationError>(rejected.Error).Errors["target"]);
        Assert.NotEqual(MemberErrors.AmbiguousCode, ErrorTexts.Get(MemberErrors.AmbiguousCode));
    }

    /// <summary>
    /// <c>users.invite</c> no trae <c>companies.read</c> ni <c>roles.read</c>, y el diálogo «Invitar usuario» lista las
    /// empresas y los roles con las rutas que los piden: la fuente tampoco los resuelve, ni los lista, ni los nombra (0068
    /// §5). Para ella no existen, así el modelo no prueba nombres, y la propuesta del Dueño le queda «ya no disponible» sin
    /// mostrar sus nombres.
    /// </summary>
    [Fact]
    public async Task Who_cannot_see_companies_or_roles_cannot_resolve_list_or_see_them()
    {
        var scenario = await new OrganizationUserActionScenarios().PrepareAsync(factory, "users.invite@1", Ct);
        using var owner = scenario.Client;
        var inviter = await OrganizationUserActionScenarios.CreateRoleAsync(owner, "Invitadora", Permissions.Users.Invite, null, Ct);
        using var anaClient = AssistantTestData.Client(factory);
        var ana = await AccountJourney.RegisterAsync(factory, anaClient, Ct);
        var anaMember = await OrganizationJourney.AddActiveMemberAsync(factory, scenario.TenantId, ana.UserId, Ct);
        await OrganizationUserActionScenarios.AssignAsync(owner, anaMember, [inviter], Ct);
        await AccountJourney.AuthorizeAsync(anaClient, "business", Ct, scenario.TenantId);
        var anaActor = await AssistantExecutionActorTests.CaptureAsync(anaClient);
        var ownerActor = await AssistantExecutionActorTests.CaptureAsync(owner);
        var email = Input(OrganizationUserActionSource.EmailField, OrganizationUserActionScenarios.Email);

        foreach (var company in new[] { "Delta", "a", "Empresa que no existe" })
        {
            var inCompany = new AssistantActionScenarioCase(null, [email, Input(OrganizationUserActionSource.CompanyField, company)]);
            var rejected = await RunAsync(anaActor, "users.invite@1", (source, definition, ct) => ProposeAsync(source, definition, inCompany, ct));
            Assert.True(rejected.IsFailure, company);
            Assert.Equal(new[] { CompanyErrors.NotFoundCode }, Assert.IsType<ValidationError>(rejected.Error).Errors[OrganizationUserActionSource.CompanyField]);
        }
        var withRoles = new AssistantActionScenarioCase(null, [email, Input(OrganizationUserActionSource.RolesField, new[] { "Ventas mayoristas" })]);
        var notSeen = await RunAsync(anaActor, "users.invite@1", (source, definition, ct) => ProposeAsync(source, definition, withRoles, ct));
        Assert.Equal(new[] { RoleErrors.NotFoundCode }, Assert.IsType<ValidationError>(notSeen.Error).Errors[OrganizationUserActionSource.RolesField]);
        var listed = await RunAsync(anaActor, "users.invite@1", (source, definition, ct) =>
            source.ListChoicesAsync(new AssistantActionChoiceRequest(definition, OrganizationUserActionSource.RolesField, null, 1, null), ct));
        Assert.True(listed.IsSuccess, listed.IsFailure ? listed.Error.Code : null);
        Assert.Empty(listed.Value.Items);

        var byOwner = await RunAsync(ownerActor, "users.invite@1", (source, definition, ct) => ProposeAsync(source, definition, scenario.Valid, ct));
        Assert.True(byOwner.IsSuccess, byOwner.IsFailure ? Describe(byOwner.Error) : null);
        var reviewed = await RunAsync(anaActor, "users.invite@1", (source, _, ct) => source.ReviewAsync(byOwner.Value, ct));
        Assert.True(reviewed.IsFailure);
        Assert.Equal(ErrorType.NotFound, reviewed.Error.Type);
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
        Func<IAssistantActionSource, AssistantActionDefinition, CancellationToken, Task<T>> work, IServiceProvider? root = null)
    {
        using var scope = (root ?? factory.Services).CreateScope();
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
