using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantExecutionActorSecurityTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static JsonElement Empty => JsonSerializer.SerializeToElement(new { });

    [Fact]
    public async Task Exclusive_operator_captured_from_real_platform_login_has_no_personal_profile_or_tenant()
    {
        await factory.Services.SeedDatabaseAsync(Ct);
        var email = Email.Create("actor-operator-" + Guid.NewGuid().ToString("N") + "@example.test").Value;
        Guid owner;
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var accounts = services.GetRequiredService<IUserRepository>();
                var user = await accounts.CreateAsync("Exclusive background operator", "es-AR", "America/Argentina/Buenos_Aires", ct);
                await accounts.SetPrimaryEmailAsync(user.Id, email, ct);
                var method = LoginMethod.CreateEmail(user.Id, email);
                Assert.True(method.Verify(services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime).IsSuccess);
                Assert.True(method.MakePrimary().IsSuccess);
                services.GetRequiredService<ILoginMethodRepository>().Add(method);
                return Result.Success(user.Id);
            }, CommitPolicy.OnSuccess, Ct);
            owner = created.Value;
        }
        await PlatformOperators.GrantAsync(factory.Services, owner, [PlatformPermissions.Assistant.Use, PlatformPermissions.Accounts.Read], Ct);
        using var client = AssistantTestData.Client(factory);
        using (var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = email.Value }, Ct))
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(factory.Services, email.Value, Ct);
        using (var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email = email.Value, code, returnUrl = "/connect/authorize?access=platform" }, Ct))
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        using var snapshot = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var documents = snapshot!.RootElement.GetProperty("pendingLegalDocuments").EnumerateArray()
            .Select(row => new { id = row.GetProperty("id").GetGuid(), version = row.GetProperty("version").GetInt32() }).ToArray();
        using (var accepted = await AccountJourney.PostAsync(client, "/api/legal/accept", new { documents }, Ct))
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, "/api/platform/assistant/notice");
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        Assert.Equal(owner, actor.OwnerUserId);
        Assert.Equal(Access.Platform, actor.Access);
        Assert.Null(actor.TenantId);
        Assert.Null(actor.TenantKind);
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        Assert.Empty(await provider.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(owner, Ct));
        var prepared = await provider.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        Assert.True(prepared.IsSuccess);
        using (provider.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value))
        {
            Assert.Null(provider.GetRequiredService<ITenantContext>().TenantId);
            Assert.Equal((AuditActorKind.PlatformOperator, (Guid?)owner), AuditActorResolver.Resolve(provider.GetRequiredService<ICurrentUser>()));
            var tools = provider.GetRequiredService<IEnumerable<IAssistantTool>>();
            Assert.True((await tools.Single(row => row.Key == "platform_accounts").ExecuteAsync(JsonSerializer.SerializeToElement(new { page = 1, pageSize = 20 }), Ct)).IsSuccess);
            Assert.True((await tools.Single(row => row.Key == "my_account").ExecuteAsync(Empty, Ct)).IsFailure);
        }
        Assert.True((await provider.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor with
            { Access = Access.Consumer, TenantId = Guid.NewGuid(), TenantKind = TenantKind.Personal }, Ct)).IsFailure);
    }

    [Fact]
    public async Task Productive_DI_rejects_existing_base_or_technical_scope_and_open_transaction_before_actor_binding()
    {
        using var client = AssistantTestData.Client(factory);
        await AccountJourney.RegisterAsync(factory, client, Ct);
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        using (var initialized = factory.Services.CreateScope())
        {
            var services = initialized.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(actor.TenantId!.Value, TenantKind.Personal);
            Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<IExecutionActorScope>().Enter(actor));
            await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct));
        }
        using (var technical = factory.Services.CreateScope())
        {
            var services = technical.ServiceProvider;
            using (services.GetRequiredService<ITenantScope>().Enter(actor.TenantId!.Value))
            {
                Assert.Null(services.GetRequiredService<ITenantContext>().TenantKind);
                Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<IExecutionActorScope>().Enter(actor));
            }
            Assert.Null(services.GetRequiredService<ITenantContext>().TenantId);
        }
        using (var transactional = factory.Services.CreateScope())
        {
            var services = transactional.ServiceProvider;
            await using var transaction = await services.GetRequiredService<ApplicationDbContext>().Database.BeginTransactionAsync(Ct);
            Assert.Throws<InvalidOperationException>(() => services.GetRequiredService<IExecutionActorScope>().Enter(actor));
            await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct));
            Assert.Null(services.GetRequiredService<IExecutionActorContext>().Actor);
        }
        var disposable = factory.Services.CreateScope();
        var actors = disposable.ServiceProvider.GetRequiredService<IExecutionActorContext>();
        var actorScopes = disposable.ServiceProvider.GetRequiredService<IExecutionActorScope>();
        var entered = actorScopes.Enter(actor);
        Assert.NotNull(actors.Actor);
        disposable.Dispose();
        Assert.Null(actors.Actor);
        Assert.Throws<ObjectDisposedException>(() => actorScopes.Enter(actor));
        entered.Dispose();
    }

    [Fact]
    public async Task Factory_uses_catalog_validated_account_preferences_instead_of_caller_supplied_values()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var prepared = await provider.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor with { Culture = "forged", TimeZoneId = "forged" }, Ct);
        Assert.Equal("es-AR", prepared.Value.Culture);
        Assert.Equal("America/Argentina/Buenos_Aires", prepared.Value.TimeZoneId);
        using (var mutation = factory.Services.CreateScope())
        {
            var services = mutation.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                var row = (await users.GetByIdAsync(account.UserId, ct))!;
                Assert.True((await users.UpdateProfileAsync(account.UserId, null, null, "missing-catalog-reference", row.TimeZoneId, row.ProfileVersion, ct)).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        Assert.True((await provider.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct)).IsFailure);
    }

    [Fact]
    public async Task New_legal_revision_blocks_an_already_captured_actor_until_real_explicit_consent()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = AssistantTestData.Client(isolated);
        await AccountJourney.RegisterAsync(isolated, client, Ct);
        await AssistantTestData.PersonalAsync(isolated, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        using var scope = isolated.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var bound = await provider.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        using (provider.GetRequiredService<IExecutionActorScope>().Enter(bound.Value))
        {
            var tool = provider.GetRequiredService<IEnumerable<IAssistantTool>>().Single(row => row.Key == "my_account");
            Assert.True((await tool.ExecuteAsync(Empty, Ct)).IsSuccess);
            LegalDocument document;
            using (var publication = isolated.Services.CreateScope())
            {
                var services = publication.ServiceProvider;
                document = LegalDocument.Create(LegalDocumentKind.Terms, 3, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
                {
                    var repository = services.GetRequiredService<ILegalRepository>();
                    repository.AddDocument(document);
                    repository.AddContent(LegalDocumentContent.Create(document.Id, "es-AR", "Nueva revisión de fixture."));
                    repository.AddContent(LegalDocumentContent.Create(document.Id, "en-US", "New fixture revision."));
                    return Task.FromResult(Result.Success());
                }, CommitPolicy.OnSuccess, Ct);
            }
            Assert.True((await tool.ExecuteAsync(Empty, Ct)).IsFailure);
            using (var accepted = await AccountJourney.PostAsync(client, "/api/legal/accept", new { documents = new[] { new { id = document.Id, version = 3 } } }, Ct))
                Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            Assert.True((await tool.ExecuteAsync(Empty, Ct)).IsSuccess);
        }
    }

    [Fact]
    public async Task Notice_revision_changed_during_real_acceptance_read_denies_the_old_revision_then_allows_new_explicit_consent()
    {
        var revision = new NoticeRevision();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var original = services.Single(row => row.ServiceType == typeof(IAssistantNoticeReader));
            services.Replace(ServiceDescriptor.Scoped<IAssistantNoticeReader>(provider => new RevisionReader(
                (IAssistantNoticeReader)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!), revision)));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await AccountJourney.RegisterAsync(host, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        var actor = await AssistantExecutionActorTests.CaptureAsync(client);
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        var bound = await provider.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        using (provider.GetRequiredService<IExecutionActorScope>().Enter(bound.Value))
        {
            var tool = provider.GetRequiredService<IEnumerable<IAssistantTool>>().Single(row => row.Key == "my_account");
            Assert.True((await tool.ExecuteAsync(Empty, Ct)).IsSuccess);
            revision.ChangeDuringRead = true;
            Assert.True((await tool.ExecuteAsync(Empty, Ct)).IsFailure);
            using (var accepted = await AccountJourney.PostAsync(client, "/api/me/assistant/notice", new { noticeVersion = "2" }, Ct))
                Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
            Assert.True((await tool.ExecuteAsync(Empty, Ct)).IsSuccess);
        }
    }

    private sealed class NoticeRevision
    {
        internal string Version { get; set; } = "1";
        internal bool ChangeDuringRead { get; set; }
    }
    private sealed class RevisionReader(IAssistantNoticeReader inner, NoticeRevision revision) : IAssistantNoticeReader
    {
        public string CurrentVersion => revision.Version;
        public async Task<bool> ExistsAsync(Guid ownerUserId, string noticeVersion, CancellationToken ct)
        {
            var exists = await inner.ExistsAsync(ownerUserId, noticeVersion, ct);
            if (revision.ChangeDuringRead) { revision.ChangeDuringRead = false; revision.Version = "2"; }
            return exists;
        }
    }
}
