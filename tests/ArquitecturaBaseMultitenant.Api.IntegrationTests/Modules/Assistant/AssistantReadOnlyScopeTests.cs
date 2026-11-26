using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools.Business;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// ADR 0068 §5 (B2): las herramientas y las fuentes corren en un alcance de DI propio, con el actor del worker o el acceso de
/// la petición, en una transacción READ ONLY que se descarta.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantReadOnlyScopeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("pending")]
    [InlineData("unitOfWork")]
    [InlineData("saveChanges")]
    public async Task A_source_that_writes_through_a_repository_leaves_no_rows(string save)
    {
        using var client = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Read-only writes", Ct);
        var name = "Read-only " + Guid.NewGuid().ToString("N");
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
            var readOnly = services.GetRequiredService<IAssistantReadOnlyScope>();
            Task<bool> Run() => readOnly.RunAsync<IServiceProvider, bool>((inner, ct) =>
                ActivatorUtilities.CreateInstance<WritingSource>(inner).WriteAsync(name, save, ct), Ct);
            switch (save)
            {
                case "pending":
                    Assert.True(await Run());
                    break;
                case "unitOfWork":
                    await Assert.ThrowsAsync<InvalidOperationException>(Run);
                    break;
                default:
                    // La base rechaza la escritura: la transacción del alcance es READ ONLY.
                    var rejected = await Assert.ThrowsAnyAsync<Exception>(Run);
                    Assert.Contains(Chain(rejected), exception => exception is PostgresException { SqlState: PostgresErrorCodes.ReadOnlySqlTransaction });
                    break;
            }
            // Lo que la fuente dejó en memoria no llega a la transacción siguiente del alcance que la llamó.
            var committed = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
                _ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, Ct);
            Assert.True(committed.IsSuccess);
        }
        using var check = factory.Services.CreateScope();
        check.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
        Assert.False(await check.ServiceProvider.GetRequiredService<ApplicationDbContext>().Roles.AnyAsync(role => role.Name == name, Ct));
    }

    [Fact]
    public async Task A_request_enters_with_its_access_tenant_and_a_reader_of_another_organization_is_empty()
    {
        using var client = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var own = await OrganizationJourney.StageAsync(factory, owner, "Read-only request own", Ct);
        var other = await OrganizationJourney.StageAsync(factory, owner, "Read-only request other", Ct);
        var foreignRole = await AdminRoleAsync(other);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(own, TenantKind.Business);
        var read = await services.GetRequiredService<IAssistantReadOnlyScope>().RunAsync<IServiceProvider, ScopeRead>(
            (inner, ct) => ReadAsync(inner, foreignRole, ct), Ct);
        Assert.Equal(own, read.TenantId);
        Assert.Equal(TenantKind.Business, read.TenantKind);
        Assert.Equal(own.ToString("D"), read.SessionTenant);
        Assert.Equal("on", read.ReadOnly);
        Assert.Null(read.ForeignRole);
        Assert.DoesNotContain(foreignRole, read.VisibleRoles);
        Assert.NotEmpty(read.VisibleRoles);
        Assert.Equal(0, read.ForeignRows);
        Assert.Null(services.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction);
    }

    [Fact]
    public async Task A_worker_enters_with_its_revalidated_actor_and_keeps_its_own_scope_untouched()
    {
        using var client = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var own = await OrganizationJourney.StageAsync(factory, owner, "Read-only worker own", Ct);
        var other = await OrganizationJourney.StageAsync(factory, owner, "Read-only worker other", Ct);
        var foreignRole = await AdminRoleAsync(other);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, own);
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var prepared = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        Assert.True(prepared.IsSuccess, prepared.Error.Code);
        using (services.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value))
        {
            var name = "Read-only worker " + Guid.NewGuid().ToString("N");
            var read = await services.GetRequiredService<IAssistantReadOnlyScope>().RunAsync<IServiceProvider, ScopeRead>(async (inner, ct) =>
            {
                Assert.Equal(owner.UserId, inner.GetRequiredService<ICurrentUser>().UserId);
                Assert.Equal(Access.Business, inner.GetRequiredService<ICurrentUser>().Access);
                Assert.True(await ActivatorUtilities.CreateInstance<WritingSource>(inner).WriteAsync(name, "pending", ct));
                return await ReadAsync(inner, foreignRole, ct);
            }, Ct);
            Assert.Equal(own, read.TenantId);
            Assert.Equal(TenantKind.Business, read.TenantKind);
            Assert.Equal(own.ToString("D"), read.SessionTenant);
            Assert.Equal("on", read.ReadOnly);
            Assert.Null(read.ForeignRole);
            Assert.DoesNotContain(foreignRole, read.VisibleRoles);
            Assert.Equal(0, read.ForeignRows);
            // El alcance del worker sigue con su actor y sin transacción: su guardado no recibe lo que quedó en memoria adentro.
            Assert.Equal(owner.UserId, services.GetRequiredService<ICurrentUser>().UserId);
            Assert.Null(services.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction);
            Assert.True((await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(
                _ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, Ct)).IsSuccess);
            Assert.False(await services.GetRequiredService<ApplicationDbContext>().Roles.AnyAsync(role => role.Name == name, Ct));
        }
    }

    [Fact]
    public async Task A_technical_tenant_scope_is_never_inherited()
    {
        using var client = AssistantTestData.Client(factory);
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Read-only technical", Ct);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var entered = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IAssistantReadOnlyScope>()
            .RunAsync<IServiceProvider, bool>((_, _) => Task.FromResult(true), Ct));
    }

    [Fact]
    public async Task The_worker_and_a_quick_query_run_their_tool_read_only_with_the_tenant_and_person_of_who_asked()
    {
        var probe = new ProbeLog();
        await using var session = await AssistantTurnTestSession.CreateAsync("business", workerEnabled: false,
            configure: builder => builder.ConfigureTestServices(services => Probe(services, probe)));
        var tenantId = session.TenantId!.Value;

        using var quick = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/quick/active_users", "{}", Guid.NewGuid());
        using var answered = await session.Client.SendAsync(quick, Ct);
        Assert.Equal(HttpStatusCode.OK, answered.StatusCode);
        var fromQuick = Assert.Single(probe.Reads);
        AssertReadOnly(fromQuick, tenantId, session.Account.UserId);

        var metrics = new AssistantFakeChatMetrics();
        using var host = session.Host.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Assistant:TurnWorkerEnabled", "true");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClientFactory>();
                services.AddScoped<IChatClientFactory>(provider => new FakeClients(metrics, provider.GetRequiredService<TimeProvider>()));
            });
        });
        using var client = await session.LoginReplicaAsync(host);
        using var create = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + "/conversations", "{}", Guid.NewGuid());
        using var created = await client.SendAsync(create, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(AssistantTurnTestSession.Json, Ct))!;
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, session.Path + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation, "[E2E:tool] Consulta de usuarios"), Guid.NewGuid());
        using var response = await client.SendAsync(send, HttpCompletionOption.ResponseHeadersRead, Ct).WaitAsync(TimeSpan.FromSeconds(20), Ct);
        using var stream = await AssistantSseTestReader.OpenAsync(response, Ct);
        AssistantSseTestReader.Frame terminal;
        do terminal = await stream.NextAsync(Ct);
        while (terminal.Event is not ("done" or "error"));
        Assert.Equal("done", terminal.Event);
        Assert.Equal(1, metrics.ToolCycles);
        Assert.Equal(2, probe.Reads.Count);
        AssertReadOnly(probe.Reads[^1], tenantId, session.Account.UserId);
    }

    private static IEnumerable<Exception> Chain(Exception? exception)
    {
        for (; exception is not null; exception = exception.InnerException) yield return exception;
    }

    private static void AssertReadOnly(ProbeRead read, Guid tenantId, Guid userId)
    {
        Assert.Equal(tenantId, read.TenantId);
        Assert.Equal(userId, read.UserId);
        Assert.Equal(tenantId.ToString("D"), read.SessionTenant);
        Assert.Equal("on", read.ReadOnly);
        Assert.True(read.InTransaction);
    }

    private async Task<Guid> AdminRoleAsync(Guid tenantId)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
        return (await scope.ServiceProvider.GetRequiredService<IRoleReader>().ListAllAsync(Ct))[0].Id;
    }

    private static async Task<ScopeRead> ReadAsync(IServiceProvider services, Guid foreignRole, CancellationToken ct)
    {
        var tenant = services.GetRequiredService<ITenantContext>();
        var db = services.GetRequiredService<ApplicationDbContext>();
        var roles = services.GetRequiredService<IRoleReader>();
        // Sin el filtro de EF: la fila ajena la esconde la RLS de la base, con la organización del alcance.
        var foreignRows = await db.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM tenant."Roles" WHERE "Id" = {foreignRole}""")
            .SingleAsync(ct);
        return new ScopeRead(tenant.TenantId, tenant.TenantKind, await SettingAsync(db, "app.tenant_id", ct),
            await SettingAsync(db, "transaction_read_only", ct), await roles.FindByIdAsync(foreignRole, ct),
            [.. (await roles.ListAllAsync(ct)).Select(role => role.Id)], foreignRows);
    }

    private static Task<string> SettingAsync(ApplicationDbContext db, string name, CancellationToken ct) =>
        db.Database.SqlQuery<string>($"""SELECT current_setting({name}) AS "Value" """).SingleAsync(ct);

    private static void Probe(IServiceCollection services, ProbeLog log)
    {
        services.Remove(services.Single(service => service.ServiceType == typeof(IAssistantTool)
            && service.ImplementationType == typeof(OrganizationUsersTool)));
        services.AddScoped<IAssistantTool>(provider =>
            new ProbeTool(ActivatorUtilities.CreateInstance<OrganizationUsersTool>(provider), provider, log));
    }

    private sealed record ScopeRead(Guid? TenantId, TenantKind? TenantKind, string SessionTenant, string ReadOnly,
        Application.Models.Roles.RoleRow? ForeignRole, IReadOnlyList<Guid> VisibleRoles, int ForeignRows);

    private sealed record ProbeRead(Guid? TenantId, Guid? UserId, string SessionTenant, string ReadOnly, bool InTransaction);

    private sealed class ProbeLog
    {
        private readonly ConcurrentQueue<ProbeRead> _reads = new();
        internal IReadOnlyList<ProbeRead> Reads => [.. _reads];
        internal void Add(ProbeRead read) => _reads.Enqueue(read);
    }

    /// <summary>La herramienta de siempre, que antes anota en qué alcance la corrieron.</summary>
    private sealed class ProbeTool(IAssistantTool inner, IServiceProvider services, ProbeLog log) : IAssistantTool
    {
        public string Key => inner.Key;
        public Access? Access => inner.Access;
        public IReadOnlyList<string> Permissions => inner.Permissions;
        public string DescriptionKey => inner.DescriptionKey;
        public JsonElement Schema => inner.Schema;
        public Task<bool> IsAvailableAsync(CancellationToken cancellationToken) => inner.IsAvailableAsync(cancellationToken);

        public async Task<Result<AssistantToolData>> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            log.Add(new ProbeRead(services.GetRequiredService<ITenantContext>().TenantId, services.GetRequiredService<ICurrentUser>().UserId,
                await SettingAsync(db, "app.tenant_id", cancellationToken), await SettingAsync(db, "transaction_read_only", cancellationToken),
                db.Database.CurrentTransaction is not null));
            return await inner.ExecuteAsync(arguments, cancellationToken);
        }
    }

    /// <summary>Una fuente de prueba que escribe: suma un rol con el repositorio y lo guarda como se le pida.</summary>
    private sealed class WritingSource(IRoleRepository roles, IUnitOfWork unitOfWork, ApplicationDbContext db)
    {
        internal async Task<bool> WriteAsync(string name, string save, CancellationToken ct)
        {
            roles.Add(Role.Create(name, null, RoleScope.Organization, null, [Permissions.Users.Read]).Value);
            if (save == "unitOfWork")
                await unitOfWork.ExecuteInTransactionAsync(_ => Task.FromResult(Result.Success()), CommitPolicy.OnSuccess, ct);
            else if (save == "saveChanges")
                await db.SaveChangesAsync(ct);
            return true;
        }
    }

    private sealed class FakeClients(AssistantFakeChatMetrics metrics, TimeProvider clock) : IChatClientFactory
    {
        public Task<Result<IChatClient>> CreateAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success<IChatClient>(new FakeChatClient(metrics, clock)));
        }

        public Task<Result> ValidateKeyAsync(string key, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Key validation is outside the read-only scenario.");
    }
}
