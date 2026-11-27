using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantActions;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Actions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// La batería genérica de las acciones (ADR 0068, «Verificación»): una teoría por acción registrada, con el escenario de su
/// área en <c>TestFeatures/AssistantActions/</c>, y sin tests por acción. Las herramientas corren como en el turno, con el
/// actor revalidado y en el alcance de solo lectura, con una calificación de prueba (la del modelo falso), y la pantalla
/// guarda por su ruta con lo que muestra la propuesta. Prueba cuándo aparece, el aislamiento, que nada escribe, la ruta con la
/// auditoría de la persona, los efectos, la revocación, la fuente que intenta escribir, versiones y turnos y el guardado único.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantActionContractTests(ApiFactory factory)
{
    private const string Root = "/api/organization/assistant/proposals/";
    private const string Model = "assistant-action-battery";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly IAssistantActionScenarioSource[] Scenarios = [new OrganizationUserActionScenarios(), new RoleActionScenarios(), new FormCatalogActionScenarios()];

    public static TheoryData<string> Actions() => [.. Scenarios.SelectMany(source => source.Actions)];

    [Fact]
    public void Every_registered_action_has_its_scenario_and_the_published_contract_is_the_one_of_the_build()
    {
        using var scope = factory.Services.CreateScope();
        var sources = scope.ServiceProvider.GetServices<IAssistantActionSource>().ToArray();
        Assert.Equal(sources.SelectMany(source => source.Definitions).Select(action => action.Key).Order(StringComparer.Ordinal),
            Scenarios.SelectMany(source => source.Actions).Order(StringComparer.Ordinal));
        var published = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "contracts", AssistantActionContractWriter.FileName))
            .ReplaceLineEndings("\n");
        Assert.Equal(AssistantActionContractWriter.Render(sources), published);
    }

    [Theory]
    [MemberData(nameof(Actions))]
    public async Task The_action_is_offered_isolated_proposed_without_writing_and_saved_once_by_its_route(string action)
    {
        var scenario = await Scenarios.Single(source => source.Actions.Contains(action)).PrepareAsync(factory, action, Ct);
        using var client = scenario.Client;
        await AssistantTestData.EnableAsync(factory, scenario.TenantId);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/organization/assistant/notice");
        var owner = await AssistantExecutionActorTests.CaptureAsync(client);
        var definition = Definition(action);

        // Cuándo aparece: a quien tiene el requisito, en la organización con el asistente.
        Assert.Contains(action, await OfferedAsync(owner));

        // Sin escribir: cada caso vuelve aceptado o con su ruta, como dato que no corta el turno, y no queda ninguna fila nueva.
        var before = await CountAsync(scenario.TenantId);
        AssistantActionToolData? valid = null;
        foreach (var (name, @case) in scenario.Cases)
        {
            var proposed = await ProposeAsync(owner, Arguments(action, @case));
            if (@case.RejectedPath is { } rejectedPath)
            {
                Assert.True(proposed.Outcome == AssistantActionToolData.Rejected, name);
                Assert.True(proposed.Errors?.Any(error => error.Path == rejectedPath) == true,
                    $"{name}: {proposed.Code} {string.Join(", ", proposed.Errors?.Select(error => error.Path) ?? [])}");
                continue;
            }
            Assert.True(proposed.Outcome == AssistantActionToolData.Accepted,
                $"{name}: {proposed.Code} {string.Join(", ", proposed.Errors?.Select(error => $"{error.Path}={error.Code}") ?? [])}");
            if (name == "valid") valid = proposed;
        }

        // Versiones y turnos: una versión desconocida no se ofrece y una segunda propuesta en el turno se rechaza.
        var unknown = await ProposeAsync(owner, Arguments($"{definition.Id}@{definition.Version + 8}", scenario.Valid));
        Assert.Equal(AssistantErrors.ActionNotAvailableCode, unknown.Code);
        var second = await ProposeAsync(owner, Arguments(action, scenario.Valid), proposed: true);
        Assert.Equal(AssistantErrors.ActionOnePerTurnCode, second.Code);

        // La fuente: en su alcance, una escritura falla.
        await RunAsync(owner, async (services, ct) =>
        {
            var source = services.GetServices<IAssistantActionSource>().Single(item => item.Definitions.Contains(definition));
            var resolved = await source.ResolveAsync(new AssistantActionDraft(definition, scenario.Valid.Target, scenario.Valid.Values), ct);
            Assert.True(resolved.IsSuccess, resolved.IsFailure ? resolved.Error.Code : null);
            await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IUnitOfWork>()
                .ExecuteInTransactionAsync(_ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, ct));
            return true;
        });
        Assert.Equal(before, await CountAsync(scenario.TenantId));

        // La propuesta aceptada, como la guarda el turno, se abre por id con lo que muestra la pantalla.
        var resolution = valid!.Resolution!;
        var conversation = await AssistantTestData.ConversationAsync(factory, scenario.TenantId, scenario.Requester.UserId, Access.Business);
        var proposal = await AssistantTestData.ProposalAsync(factory, scenario.TenantId, conversation.Id, scenario.Requester.UserId,
            definition.Id, definition.Version, new AssistantProposalValues(valid.WrittenTarget, resolution.Values), resolution.TargetId,
            resolution.BaseVersion, resolution.SubjectUserIds);
        var path = Root + proposal.Id.ToString("D");
        var review = await ReviewAsync(owner, resolution);
        await AssertStateAsync(client, path, "new", null);
        var stale = await AssistantTestData.ProposalAsync(factory, scenario.TenantId, conversation.Id, scenario.Requester.UserId,
            definition.Id, definition.Version + 8, new AssistantProposalValues(valid.WrittenTarget, resolution.Values), resolution.TargetId,
            resolution.BaseVersion, resolution.SubjectUserIds);
        await AssertStateAsync(client, Root + stale.Id.ToString("D"), "unavailable", "version");

        // La revocación: quien tenía el permiso y lo perdió en el medio ya no la ve, y la ruta le da 403.
        await AssertRevokedAsync(scenario, definition, review);

        // La ruta guarda una sola vez, con la auditoría de la persona, y sin efectos que la acción no declara.
        var outbox = await OutboxAsync();
        var audits = await AuditsAsync(scenario.TenantId);
        if (definition.Kind == AssistantActionKind.Create)
        {
            // Un 422 con otro cuerpo no se toma como guardado: la clave terminó en 4xx y va otra.
            var first = await AttemptAsync(client, path);
            using (var invalid = await AssistantActionRequests.PostAsync(client, RouteOf(definition), new { }, Guid.Parse(first), Ct))
                Assert.True((int)invalid.StatusCode is >= 400 and < 500, $"{(int)invalid.StatusCode}");
            using (var reused = await scenario.SaveAsync(client, review, Guid.Parse(first), Ct))
                Assert.True(reused.StatusCode == HttpStatusCode.UnprocessableEntity, $"{(int)reused.StatusCode}: {await reused.Content.ReadAsStringAsync(Ct)}");
            await AssertStateAsync(client, path, "new", null);
            var key = await AttemptAsync(client, path);
            Assert.NotEqual(first, key);
            using (var saved = await scenario.SaveAsync(client, review, Guid.Parse(key), Ct))
                Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync(Ct));
            await AssertSavedAsync(scenario, definition, outbox, audits);
            // Otra pestaña con la misma propuesta: la misma clave no crea otro, y la propuesta dice que ya se guardó.
            audits = await AuditsAsync(scenario.TenantId);
            using (var again = await scenario.SaveAsync(client, review, Guid.Parse(key), Ct))
                Assert.True(again.IsSuccessStatusCode, await again.Content.ReadAsStringAsync(Ct));
            Assert.Equal(audits, await AuditsAsync(scenario.TenantId));
        }
        else
        {
            using (var saved = await scenario.SaveAsync(client, review, null, Ct))
                Assert.True(saved.IsSuccessStatusCode, await saved.Content.ReadAsStringAsync(Ct));
            await AssertSavedAsync(scenario, definition, outbox, audits);
            // Otra pestaña con la misma propuesta: la versión que mostró ya no es la de ahora, y no guarda otra vez.
            audits = await AuditsAsync(scenario.TenantId);
            using (var again = await scenario.SaveAsync(client, review, null, Ct))
                Assert.False(again.IsSuccessStatusCode, $"{(int)again.StatusCode}");
            Assert.Equal(audits, await AuditsAsync(scenario.TenantId));
            uint? now = null;
            if (definition.Kind == AssistantActionKind.Edit)
            {
                using var shown = await ReadAsync(client, path);
                now = shown.RootElement.GetProperty("target").GetProperty("version").GetUInt32();
                Assert.NotEqual(resolution.BaseVersion, now);
            }
            using var applied = await client.PostAsJsonAsync(path + "/applied", new { version = now }, Ct);
            Assert.True(applied.StatusCode == HttpStatusCode.OK, await applied.Content.ReadAsStringAsync(Ct));
        }
        using (var attempt = await client.PostAsJsonAsync(path + "/attempts", new { }, Ct))
            await AssertProblemAsync(attempt, HttpStatusCode.Conflict, AssistantErrors.ProposalAlreadyAppliedCode);
        await AssertStateAsync(client, path, "applied", null);

        // El aislamiento: desde otra organización, también con el asistente, la propuesta es 404.
        await AssistantTestData.EnableAsync(factory, scenario.OtherTenantId);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, scenario.OtherTenantId);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/organization/assistant/notice");
        using (var foreign = await client.GetAsync(path, Ct))
            await AssertProblemAsync(foreign, HttpStatusCode.NotFound, AssistantErrors.ProposalNotFoundCode);
        // Con el módulo apagado no se ofrece: la misma persona, con el requisito, en una organización sin el asistente.
        var withoutAssistant = await OrganizationJourney.StageAsync(factory, scenario.Requester, "Sin asistente " + definition.Id, Ct);
        // Lo apagado es el asistente: una acción de un módulo se ofrece con su módulo prendido.
        if (definition.Module is { } module) await EnableModuleAsync(withoutAssistant, module);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, withoutAssistant);
        var elsewhere = await AssistantExecutionActorTests.CaptureAsync(client);
        Assert.Contains(action, await OfferedAsync(elsewhere));
        Assert.True((await ListAsync(elsewhere)).IsFailure);
        // Desde otro acceso no aparece.
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        Assert.DoesNotContain(action, await OfferedAsync(await AssistantExecutionActorTests.CaptureAsync(client)));
    }

    // Prende el módulo de la acción en la organización, como lo deja Plataforma, con su evento de seguridad.
    private async Task EnableModuleAsync(Guid tenantId, string module)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var saved = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            services.GetRequiredService<ITenantFeatureRepository>().Add(TenantFeature.Create(tenantId, module, true, TenantFeatureSource.Manual, null).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(AuditActorKind.System, Guid.Empty, tenantId,
                "Module fixture grant", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error.Code : null);
    }

    private async Task AssertRevokedAsync(AssistantActionScenario scenario, AssistantActionDefinition definition, AssistantActionReview review)
    {
        var role = await OrganizationUserActionScenarios.CreateRoleAsync(scenario.Client, "Delegada " + definition.Id,
            definition.Requirement.Permission!, null, Ct);
        using var delegated = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, delegated, Ct);
        var member = await OrganizationJourney.AddActiveMemberAsync(factory, scenario.TenantId, account.UserId, Ct);
        await OrganizationUserActionScenarios.AssignAsync(scenario.Client, member, [role], Ct);
        await AccountJourney.AuthorizeAsync(delegated, "business", Ct, scenario.TenantId);
        var actor = await AssistantExecutionActorTests.CaptureAsync(delegated);
        Assert.Contains(definition.Key, await OfferedAsync(actor));

        await OrganizationUserActionScenarios.AssignAsync(scenario.Client, member, [], Ct);
        Assert.DoesNotContain(definition.Key, await OfferedAsync(actor));
        var audits = await AuditsAsync(scenario.TenantId);
        using var revoked = await scenario.SaveAsync(delegated, review, null, Ct);
        Assert.True(revoked.StatusCode == HttpStatusCode.Forbidden, $"{(int)revoked.StatusCode}: {await revoked.Content.ReadAsStringAsync(Ct)}");
        Assert.Equal(audits, await AuditsAsync(scenario.TenantId));
    }

    private async Task AssertSavedAsync(AssistantActionScenario scenario, AssistantActionDefinition definition, IReadOnlyCollection<Guid> outbox,
        IReadOnlyCollection<Guid> audits)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(scenario.TenantId);
        var db = services.GetRequiredService<ApplicationDbContext>();
        var written = await db.Set<AuditEntry>().AsNoTracking().Where(row => !audits.Contains(row.Id)).ToArrayAsync(Ct);
        Assert.NotEmpty(written);
        Assert.All(written, row => Assert.Equal((AuditActorKind.User, scenario.Requester.UserId), (row.ActorKind, row.ActorId!.Value)));
        if (definition.Effect == AssistantActionEffect.None)
            Assert.False(await db.OutboxMessages.AsNoTracking().AnyAsync(row => !outbox.Contains(row.Id), Ct), "An action without effects enqueued a message.");
    }

    private AssistantActionDefinition Definition(string action)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetServices<IAssistantActionSource>().SelectMany(source => source.Definitions).Single(definition => definition.Key == action);
    }

    private static string RouteOf(AssistantActionDefinition definition) => "/" + definition.SaveRoute[(definition.SaveRoute.IndexOf(' ', StringComparison.Ordinal) + 1)..];

    private static JsonElement Arguments(string action, AssistantActionScenarioCase @case)
    {
        var arguments = new Dictionary<string, object?>
        {
            ["action"] = action,
            ["values"] = @case.Values.Select(value => new { field = value.Field, value = value.Value }).ToArray(),
        };
        if (@case.Target is { } target) arguments["target"] = target;
        return JsonSerializer.SerializeToElement(arguments);
    }

    private async Task<IReadOnlyList<string>> OfferedAsync(ExecutionActor actor) =>
        await RunAsync(actor, async (services, ct) => (await Catalog(services).ListAsync(Model, ct)).Select(item => item.Key).ToArray());

    private Task<Result<AssistantToolExecution>> ListAsync(ExecutionActor actor) => RunAsync(actor, (services, ct) =>
        Builder(services).ExecuteAsync(AssistantActionTools.ListKey, JsonSerializer.SerializeToElement(new { }), Turn(services, false), ct));

    private async Task<AssistantActionToolData> ProposeAsync(ExecutionActor actor, JsonElement arguments, bool proposed = false)
    {
        var executed = await RunAsync(actor, (services, ct) =>
            Builder(services).ExecuteAsync(AssistantActionTools.ProposeKey, arguments, Turn(services, proposed), ct));
        Assert.True(executed.IsSuccess, executed.IsFailure ? executed.Error.Code : null);
        return Assert.IsType<AssistantActionToolData>(executed.Value.Action);
    }

    private async Task<AssistantActionReview> ReviewAsync(ExecutionActor actor, AssistantActionResolution resolution)
    {
        var reviewed = await RunAsync(actor, (services, ct) => services.GetServices<IAssistantActionSource>()
            .Single(source => source.Definitions.Contains(resolution.Action)).ReviewAsync(resolution, ct));
        Assert.True(reviewed.IsSuccess, reviewed.IsFailure ? reviewed.Error.Code : null);
        return reviewed.Value;
    }

    // La calificación de prueba, como la del modelo falso, con todas las acciones registradas.
    private static AssistantActionCatalog Catalog(IServiceProvider services) =>
        ActivatorUtilities.CreateInstance<AssistantActionCatalog>(services, new AssistantActionQualifications(
            [.. services.GetServices<IAssistantActionSource>().SelectMany(source => source.Definitions)
                .Select(action => new AssistantActionQualification(action.Id, action.Version, Model, new DateOnly(2026, 11, 26),
                    new Money(0.01m, CurrencyCode.Create("USD").Value)))]));

    private static AssistantTurnToolBuilder Builder(IServiceProvider services) =>
        ActivatorUtilities.CreateInstance<AssistantTurnToolBuilder>(services, new AssistantActionTools(Catalog(services)));

    private static AssistantActionTurn Turn(IServiceProvider services, bool proposed) =>
        new([.. services.GetServices<IAssistantActionSource>().SelectMany(source => source.Definitions).Select(action => action.Key)], Model, proposed);

    // Como en el turno: con el actor revalidado del worker y en el alcance de solo lectura.
    private async Task<T> RunAsync<T>(ExecutionActor actor, Func<IServiceProvider, CancellationToken, Task<T>> work)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var prepared = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        Assert.True(prepared.IsSuccess, prepared.IsFailure ? prepared.Error.Code : null);
        using (services.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value))
            return await services.GetRequiredService<IAssistantReadOnlyScope>().RunAsync(work, Ct);
    }

    private async Task<(int Audits, int Outbox)> CountAsync(Guid tenantId) => ((await AuditsAsync(tenantId)).Count, (await OutboxAsync()).Count);

    private async Task<IReadOnlyCollection<Guid>> AuditsAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await services.GetRequiredService<ApplicationDbContext>().Set<AuditEntry>().AsNoTracking().Select(row => row.Id).ToArrayAsync(Ct);
    }

    private async Task<IReadOnlyCollection<Guid>> OutboxAsync()
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().OutboxMessages.AsNoTracking().Select(row => row.Id).ToArrayAsync(Ct);
    }

    private static async Task<string> AttemptAsync(HttpClient client, string path)
    {
        using var response = await client.PostAsJsonAsync(path + "/attempts", new { }, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        return json.RootElement.GetProperty("idempotencyKey").GetString()!;
    }

    private static async Task<JsonDocument> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body);
    }

    private static async Task AssertStateAsync(HttpClient client, string path, string state, string? reason)
    {
        using var shown = await ReadAsync(client, path);
        Assert.Equal(state, shown.RootElement.GetProperty("state").GetString());
        Assert.Equal(reason, shown.RootElement.TryGetProperty("unavailableReason", out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() : null);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, $"{(int)response.StatusCode}: {body}");
        using var problem = JsonDocument.Parse(body);
        Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
