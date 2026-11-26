using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Configuration.Security;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// ADR 0068 §8 a §10: la propuesta es solo de quien la pidió (la de otra persona u organización es 404 en las tres rutas), su
/// estado se calcula al leer con la fuente y los permisos de ahora, <c>attempts</c> da la misma clave mientras el intento no
/// termine, otra después de un 4xx y <c>AlreadyApplied</c> después de un 2xx, y <c>applied</c> se comprueba en el servidor.
/// La fuente de prueba vuelve a resolver sin tocar un área real; la acción que crea guarda en <c>POST api/roles</c>, una ruta
/// <c>[Idempotent]</c> de verdad, así las claves terminan en 2xx o en 4xx como desde la pantalla.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantProposalTests(ApiFactory factory)
{
    private const string Root = "/api/organization/assistant/proposals/";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_proposal_of_another_person_or_another_organization_answers_404_on_every_route_without_effects()
    {
        await using var session = await SessionAsync();
        Guid other;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct => Result.Success(
                (await services.GetRequiredService<IUserRepository>().CreateAsync("Otra persona", "es-AR", "America/Argentina/Buenos_Aires", ct)).Id),
                CommitPolicy.OnSuccess, Ct);
            other = created.Value;
        }
        await OrganizationJourney.AddActiveMemberAsync(factory, session.TenantId, other, Ct);
        var otherConversation = await AssistantTestData.ConversationAsync(factory, session.TenantId, other, Access.Business);
        var otherPerson = await AssistantTestData.ProposalAsync(factory, session.TenantId, otherConversation.Id, other,
            ProposalTestSource.Create.Id, 1, Values());
        // La misma persona, en otra de sus organizaciones: el claim manda.
        var secondTenant = await OrganizationJourney.StageAsync(factory, session.Account, "Propuestas en otra organización", Ct);
        var secondConversation = await AssistantTestData.ConversationAsync(factory, secondTenant, session.Account.UserId, Access.Business);
        var otherOrganization = await AssistantTestData.ProposalAsync(factory, secondTenant, secondConversation.Id, session.Account.UserId,
            ProposalTestSource.Create.Id, 1, Values());

        foreach (var proposal in new[] { otherPerson.Id, otherOrganization.Id, Guid.NewGuid() })
        {
            using var read = await session.Client.GetAsync(Root + proposal.ToString("D"), Ct);
            await AssertProblemAsync(read, HttpStatusCode.NotFound, AssistantErrors.ProposalNotFoundCode);
            using var attempt = await session.Client.PostAsJsonAsync(Root + proposal.ToString("D") + "/attempts", new { }, Ct);
            await AssertProblemAsync(attempt, HttpStatusCode.NotFound, AssistantErrors.ProposalNotFoundCode);
            using var applied = await session.Client.PostAsJsonAsync(Root + proposal.ToString("D") + "/applied", new { version = 1 }, Ct);
            await AssertProblemAsync(applied, HttpStatusCode.NotFound, AssistantErrors.ProposalNotFoundCode);
        }
        Assert.Equal("[]", (await StoredAsync(session.TenantId, otherPerson.Id)).AttemptKeys);
        Assert.Equal("[]", (await StoredAsync(secondTenant, otherOrganization.Id)).AttemptKeys);
        Assert.Equal(AssistantProposalStatus.New, (await StoredAsync(secondTenant, otherOrganization.Id)).State);
    }

    [Fact]
    public async Task Every_state_and_every_unavailable_reason_is_calculated_when_read()
    {
        await using var session = await SessionAsync();
        using var subjectClient = AssistantTestData.Client(factory);
        var subject = await AccountJourney.RegisterAsync(factory, subjectClient, Ct);
        using (var deletion = await AccountJourney.PostAsync(subjectClient, "/api/me/deletion", new { reason = "Baja de un sujeto de la propuesta" }, Ct))
            Assert.Equal(HttpStatusCode.OK, deletion.StatusCode);
        // La baja empezó: el trabajo de fondo tomó la cuenta (lo que mira el guardia de los sujetos).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var started = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var user = await db.Users.SingleAsync(row => row.Id == subject.UserId, ct);
                db.Entry(user).Property(row => row.DeletionLeaseId).CurrentValue = Guid.NewGuid();
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(started.IsSuccess, started.Error.Code);
        }
        var validity = factory.Services.GetRequiredService<IOptions<IdempotencyOptions>>().Value.ValidityHours;
        var conversation = await AssistantTestData.ConversationAsync(factory, session.TenantId, session.Account.UserId, Access.Business);
        var target = Guid.NewGuid();
        Task<AssistantProposal> Seed(AssistantActionDefinition action, int? version = null, Guid? targetId = null, uint? baseVersion = null,
            IReadOnlyCollection<Guid>? subjects = null, Action<AssistantProposal, DateTime>? shape = null) =>
            AssistantTestData.ProposalAsync(factory, session.TenantId, conversation.Id, session.Account.UserId, action.Id, version ?? action.Version,
                Values(), targetId, baseVersion, subjects, shape);

        var fresh = await Seed(ProposalTestSource.Edit, targetId: target, baseVersion: 3);
        var replaced = await Seed(ProposalTestSource.Create, shape: (row, _) => row.Replace());
        var applied = await Seed(ProposalTestSource.Create, shape: (row, now) => row.MarkApplied("/api/roles/hecho", now));
        var unconfirmed = await Seed(ProposalTestSource.Create, shape: (row, now) => row.AddAttemptKey(Guid.NewGuid(), now.AddHours(-validity - 1)));
        var version = await Seed(ProposalTestSource.Create, version: 9);
        var module = await Seed(ProposalTestSource.Module);
        var permission = await Seed(ProposalTestSource.Permission);
        var forbidden = await Seed(ProposalTestSource.Edit, targetId: ProposalTestSource.ForbiddenTarget, baseVersion: 1);
        var gone = await Seed(ProposalTestSource.Edit, targetId: ProposalTestSource.GoneTarget, baseVersion: 1);
        var deleted = await Seed(ProposalTestSource.Create, subjects: [subject.UserId]);

        using (var shown = await ReadAsync(session, fresh.Id))
        {
            var root = shown.RootElement;
            Assert.Equal("new", root.GetProperty("state").GetString());
            Assert.Equal(conversation.Id, root.GetProperty("conversationId").GetGuid());
            Assert.Equal("action", root.GetProperty("kind").GetString());
            Assert.Equal(ProposalTestSource.Edit.Id, root.GetProperty("action").GetString());
            Assert.Equal(1, root.GetProperty("version").GetInt32());
            Assert.Null(Text(root, "unavailableReason"));
            Assert.Equal(target, root.GetProperty("target").GetProperty("id").GetGuid());
            Assert.Equal(ProposalTestSource.TargetLabel, root.GetProperty("target").GetProperty("label").GetString());
            Assert.Equal(4u, root.GetProperty("target").GetProperty("version").GetUInt32());
            Assert.Equal(3u, root.GetProperty("baseVersion").GetUInt32());
            var value = Assert.Single(root.GetProperty("values").EnumerateArray());
            Assert.Equal("name", value.GetProperty("field").GetString());
            Assert.Equal("ventas-id", value.GetProperty("value").GetString());
            Assert.Equal(ProposalTestSource.ValueLabel, value.GetProperty("label").GetString());
        }
        await AssertStateAsync(session, replaced.Id, "replaced", null);
        using (var shown = await ReadAsync(session, applied.Id))
        {
            Assert.Equal("applied", shown.RootElement.GetProperty("state").GetString());
            Assert.Equal("/api/roles/hecho", shown.RootElement.GetProperty("appliedLocation").GetString());
        }
        await AssertStateAsync(session, unconfirmed.Id, "unconfirmed", null);
        await AssertStateAsync(session, version.Id, "unavailable", "version");
        await AssertStateAsync(session, module.Id, "unavailable", "module");
        await AssertStateAsync(session, permission.Id, "unavailable", "permission");
        await AssertStateAsync(session, forbidden.Id, "unavailable", "permission");
        await AssertStateAsync(session, gone.Id, "unavailable", "target");
        using (var shown = await ReadAsync(session, deleted.Id))
        {
            Assert.Equal("unavailable", shown.RootElement.GetProperty("state").GetString());
            Assert.Equal("subject", Text(shown.RootElement, "unavailableReason"));
            // Con una cuenta dada de baja no se muestra nada de lo resuelto.
            Assert.Empty(shown.RootElement.GetProperty("values").EnumerateArray());
            Assert.Null(Text(shown.RootElement, "target"));
        }

        // Reemplazada, sin confirmar o no disponible: attempts y applied responden el estado y el motivo, sin escribir.
        foreach (var (id, state, reason) in new[] { (replaced.Id, "replaced", (string?)null), (unconfirmed.Id, "unconfirmed", null),
            (module.Id, "unavailable", "module"), (deleted.Id, "unavailable", "subject") })
        {
            using var attempt = await session.Client.PostAsJsonAsync(Root + id.ToString("D") + "/attempts", new { }, Ct);
            using var problem = await AssertProblemAsync(attempt, HttpStatusCode.Conflict, AssistantErrors.ProposalUnavailableCode);
            Assert.Equal(state, problem.RootElement.GetProperty("state").GetString());
            Assert.Equal(reason, Text(problem.RootElement, "reason"));
        }
        using (var notAvailable = await session.Client.PostAsJsonAsync(Root + permission.Id.ToString("D") + "/applied", new { }, Ct))
            await AssertProblemAsync(notAvailable, HttpStatusCode.Conflict, AssistantErrors.ProposalUnavailableCode);
        Assert.Single((await StoredAsync(session.TenantId, unconfirmed.Id)).ReadAttemptKeys());
        Assert.Equal(AssistantProposalStatus.New, (await StoredAsync(session.TenantId, permission.Id)).State);
    }

    [Fact]
    public async Task Attempts_keep_the_key_across_tabs_give_another_after_a_4xx_and_answer_already_applied_after_a_2xx()
    {
        await using var session = await SessionAsync();
        var conversation = await AssistantTestData.ConversationAsync(factory, session.TenantId, session.Account.UserId, Access.Business);
        var proposal = await AssistantTestData.ProposalAsync(factory, session.TenantId, conversation.Id, session.Account.UserId,
            ProposalTestSource.Create.Id, 1, Values());
        var path = Root + proposal.Id.ToString("D");

        // Dos pestañas a la vez: la misma clave.
        var tabs = await Task.WhenAll(AttemptAsync(session, path), AttemptAsync(session, path));
        Assert.Equal(tabs[0], tabs[1]);
        Assert.Equal(tabs[0], await AttemptAsync(session, path));

        // El intento terminó en 4xx: la clave guardó el error y va otra.
        using (var invalid = await CreateRoleAsync(session, tabs[0], string.Empty))
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var second = await AttemptAsync(session, path);
        Assert.NotEqual(tabs[0], second);
        using (var shown = await ReadAsync(session, proposal.Id)) Assert.Equal("new", shown.RootElement.GetProperty("state").GetString());

        // El intento terminó en 2xx: el GET ya la ve aplicada, y attempts la marca y responde AlreadyApplied con el enlace.
        string location;
        using (var created = await CreateRoleAsync(session, second, "Rol de la propuesta " + Guid.NewGuid().ToString("N")[..8]))
        {
            Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
            location = created.Headers.Location!.OriginalString;
        }
        using (var shown = await ReadAsync(session, proposal.Id))
        {
            Assert.Equal("applied", shown.RootElement.GetProperty("state").GetString());
            Assert.Equal(location, shown.RootElement.GetProperty("appliedLocation").GetString());
        }
        Assert.Equal(AssistantProposalStatus.New, (await StoredAsync(session.TenantId, proposal.Id)).State);
        using (var again = await session.Client.PostAsJsonAsync(path + "/attempts", new { }, Ct))
        {
            using var problem = await AssertProblemAsync(again, HttpStatusCode.Conflict, AssistantErrors.ProposalAlreadyAppliedCode);
            Assert.Equal(location, problem.RootElement.GetProperty("location").GetString());
        }
        var stored = await StoredAsync(session.TenantId, proposal.Id);
        Assert.Equal(AssistantProposalStatus.Applied, stored.State);
        Assert.Equal(location, stored.AppliedLocation);
        Assert.Equal(new[] { tabs[0], second }, stored.ReadAttemptKeys().Select(key => key.Key.ToString("D")));
        using (var repeated = await session.Client.PostAsJsonAsync(path + "/attempts", new { }, Ct))
            await AssertProblemAsync(repeated, HttpStatusCode.Conflict, AssistantErrors.ProposalAlreadyAppliedCode);
        using (var applied = await session.Client.PostAsJsonAsync(path + "/applied", new { }, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
            using var body = JsonDocument.Parse(await applied.Content.ReadAsStringAsync(Ct));
            Assert.Equal("applied", body.RootElement.GetProperty("state").GetString());
        }
    }

    [Fact]
    public async Task Applied_is_checked_by_the_server_for_create_remove_and_edit()
    {
        await using var session = await SessionAsync();
        var conversation = await AssistantTestData.ConversationAsync(factory, session.TenantId, session.Account.UserId, Access.Business);
        var create = await AssistantTestData.ProposalAsync(factory, session.TenantId, conversation.Id, session.Account.UserId,
            ProposalTestSource.Create.Id, 1, Values());
        var remove = await AssistantTestData.ProposalAsync(factory, session.TenantId, conversation.Id, session.Account.UserId,
            ProposalTestSource.Remove.Id, 1, Values(), ProposalTestSource.GoneTarget, 2);
        var edit = await AssistantTestData.ProposalAsync(factory, session.TenantId, conversation.Id, session.Account.UserId,
            ProposalTestSource.Edit.Id, 1, Values(), Guid.NewGuid(), 3);

        // Crear sin una clave que terminara en 2xx no se puede comprobar.
        using (var unsaved = await session.Client.PostAsJsonAsync(Root + create.Id.ToString("D") + "/applied", new { }, Ct))
            await AssertProblemAsync(unsaved, HttpStatusCode.Conflict, AssistantErrors.ProposalNotAppliedCode);
        Assert.Equal(AssistantProposalStatus.New, (await StoredAsync(session.TenantId, create.Id)).State);

        // Quitar: que el destino ya no se resuelva es la prueba.
        await AssertStateAsync(session, remove.Id, "unavailable", "target");
        using (var removed = await session.Client.PostAsJsonAsync(Root + remove.Id.ToString("D") + "/applied", new { }, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
            using var body = JsonDocument.Parse(await removed.Content.ReadAsStringAsync(Ct));
            Assert.Equal("applied", body.RootElement.GetProperty("state").GetString());
        }
        Assert.Equal(AssistantProposalStatus.Applied, (await StoredAsync(session.TenantId, remove.Id)).State);

        // Editar: la versión de ahora (4) tiene que ser la del cuerpo y distinta de la de la propuesta (3).
        foreach (var stale in new uint?[] { null, 3, 5 })
        {
            using var rejected = await session.Client.PostAsJsonAsync(Root + edit.Id.ToString("D") + "/applied", new { version = stale }, Ct);
            await AssertProblemAsync(rejected, HttpStatusCode.Conflict, AssistantErrors.ProposalNotAppliedCode);
        }
        using (var edited = await session.Client.PostAsJsonAsync(Root + edit.Id.ToString("D") + "/applied", new { version = 4 }, Ct))
        {
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
            using var body = JsonDocument.Parse(await edited.Content.ReadAsStringAsync(Ct));
            Assert.Equal("applied", body.RootElement.GetProperty("state").GetString());
            Assert.Equal(4u, body.RootElement.GetProperty("target").GetProperty("version").GetUInt32());
        }
        Assert.Equal(AssistantProposalStatus.Applied, (await StoredAsync(session.TenantId, edit.Id)).State);
    }

    private static AssistantProposalValues Values() => new("Rol viejo", [AssistantTestData.Written("name", "Ventas", "ventas-id")]);

    private async Task<Session> SessionAsync()
    {
        var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IAssistantActionSource, ProposalTestSource>()));
        var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        try
        {
            var account = await AccountJourney.RegisterAsync(host, client, Ct);
            var tenantId = await OrganizationJourney.StageAsync(factory, account, "Propuestas del asistente", Ct);
            await AssistantTestData.EnableAsync(factory, tenantId);
            await AccountJourney.AuthorizeAsync(client, "business", Ct, tenantId);
            await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/organization/assistant/notice");
            return new(host, client, account, tenantId);
        }
        catch
        {
            client.Dispose();
            await host.DisposeAsync();
            throw;
        }
    }

    private static async Task<string> AttemptAsync(Session session, string path)
    {
        using var response = await session.Client.PostAsJsonAsync(path + "/attempts", new { }, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var key = body.RootElement.GetProperty("idempotencyKey").GetString()!;
        Assert.True(Guid.TryParse(key, out _));
        return key;
    }

    private static async Task<HttpResponseMessage> CreateRoleAsync(Session session, string key, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/roles")
        {
            Content = JsonContent.Create(new
            {
                name,
                description = (string?)null,
                scope = "Organization",
                companyId = (Guid?)null,
                permissions = new[] { Permissions.Assistant.Use },
            }),
        };
        request.Headers.Add("Idempotency-Key", key);
        return await session.Client.SendAsync(request, Ct);
    }

    private static async Task<JsonDocument> ReadAsync(Session session, Guid id)
    {
        using var response = await session.Client.GetAsync(Root + id.ToString("D"), Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonDocument.Parse(body);
    }

    private static async Task AssertStateAsync(Session session, Guid id, string state, string? reason)
    {
        using var shown = await ReadAsync(session, id);
        Assert.Equal(state, shown.RootElement.GetProperty("state").GetString());
        Assert.Equal(reason, Text(shown.RootElement, "unavailableReason"));
    }

    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, $"{(int)response.StatusCode}: {body}");
        var problem = JsonDocument.Parse(body);
        Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        return problem;
    }

    private static string? Text(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;

    private async Task<AssistantProposal> StoredAsync(Guid tenantId, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        return await services.GetRequiredService<ApplicationDbContext>().Set<AssistantProposal>().AsNoTracking().SingleAsync(row => row.Id == id, Ct);
    }

    private sealed class Session(WebApplicationFactory<Program> host, HttpClient client, AccountJourney account, Guid tenantId) : IAsyncDisposable
    {
        internal HttpClient Client { get; } = client;
        internal AccountJourney Account { get; } = account;
        internal Guid TenantId { get; } = tenantId;

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Una fuente de prueba que solo vuelve a resolver: el destino <see cref="GoneTarget"/> ya no existe, con
    /// <see cref="ForbiddenTarget"/> quien pide ya no puede, y cualquier otro está una versión más adelante que al proponer.
    /// </summary>
    private sealed class ProposalTestSource : IAssistantActionSource
    {
        internal const string TargetLabel = "Destino leído del servidor";
        internal const string ValueLabel = "Nombre leído del servidor";
        internal static readonly Guid GoneTarget = Guid.Parse("0b3f6d7e-5a51-4c55-9a0e-6f1f5b1c0a01");
        internal static readonly Guid ForbiddenTarget = Guid.Parse("0b3f6d7e-5a51-4c55-9a0e-6f1f5b1c0a02");

        internal static readonly AssistantActionDefinition Create = Action("proposaltest.create", AssistantActionKind.Create,
            AssistantActionRequirement.Organization(Permissions.Roles.Manage), "POST api/roles");
        internal static readonly AssistantActionDefinition Edit = Action("proposaltest.edit", AssistantActionKind.Edit,
            AssistantActionRequirement.Organization(Permissions.Roles.Manage), "PUT api/roles/{id}") with { Target = new("GET api/roles/{id}") };
        internal static readonly AssistantActionDefinition Remove = Action("proposaltest.remove", AssistantActionKind.Remove,
            AssistantActionRequirement.Organization(Permissions.Roles.Manage), "DELETE api/roles/{id}") with { Target = new("GET api/roles/{id}") };
        internal static readonly AssistantActionDefinition Module = Action("proposaltest.module", AssistantActionKind.Create,
            AssistantActionRequirement.Organization(Permissions.Roles.Manage), "POST api/roles") with { Module = WhatsAppFeature.Key };
        internal static readonly AssistantActionDefinition Permission = Action("proposaltest.permission", AssistantActionKind.Create,
            AssistantActionRequirement.Organization("proposaltest.nobody-holds-it"), "POST api/roles");

        public IReadOnlyList<AssistantActionDefinition> Definitions { get; } = [Create, Edit, Remove, Module, Permission];

        public Task<Result<AssistantActionReview>> ReviewAsync(AssistantActionResolution resolution, CancellationToken cancellationToken)
        {
            if (resolution.TargetId == GoneTarget) return Task.FromResult<Result<AssistantActionReview>>(Error.NotFound("ProposalTest.Gone", "Gone."));
            if (resolution.TargetId == ForbiddenTarget)
                return Task.FromResult<Result<AssistantActionReview>>(Error.Forbidden("ProposalTest.Forbidden", "Forbidden."));
            var target = resolution.TargetId is { } id ? new AssistantActionTargetView(id, TargetLabel, (resolution.BaseVersion ?? 0) + 1) : null;
            var values = resolution.Values.Select(value => new AssistantActionValueView(value.Field, value.Resolved ?? value.Written, ValueLabel, value.Doubt))
                .ToArray();
            return Task.FromResult<Result<AssistantActionReview>>(new AssistantActionReview(target, values));
        }

        public Task<Result<PagedResult<AssistantActionChoiceRow>>> ListChoicesAsync(AssistantActionChoiceRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The proposal source only reviews.");

        public Task<Result<AssistantActionResolution>> ResolveAsync(AssistantActionDraft draft, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The proposal source only reviews.");

        public Task<Result<IReadOnlyList<AssistantActionField>>> ReadTargetFieldsAsync(AssistantActionDefinition action, string target,
            CancellationToken cancellationToken) => throw new NotSupportedException("The proposal source only reviews.");

        public Task<Result> ValidateAsync(AssistantActionResolution resolution, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The proposal source only reviews.");

        private static AssistantActionDefinition Action(string id, AssistantActionKind kind, AssistantActionRequirement requirement, string saveRoute) => new()
        {
            Id = id,
            Version = 1,
            Kind = kind,
            Requirement = requirement,
            SaveRoute = saveRoute,
            Confirmation = AssistantActionConfirmation.Dialog,
            Screen = AssistantActionScreen.RoleEditor,
            Fields = [new() { Name = "name", Type = AssistantActionFieldType.Text, Required = true }],
        };
    }
}
