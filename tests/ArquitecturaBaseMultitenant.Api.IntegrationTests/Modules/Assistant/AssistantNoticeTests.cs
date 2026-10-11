using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.TestHost;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantNoticeTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_login_and_get_do_not_accept_the_notice_and_unknown_version_is_rejected()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        using var state = await client.GetAsync("/api/me/assistant", Ct);
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
        using var response = await SendAsync(client, "unknown", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await response.Content.ReadFromJsonAsync<JsonDocument>(Ct);
        Assert.Equal(AssistantErrors.NoticeAcceptanceRequiredCode, problem!.RootElement.GetProperty("code").GetString());
        Assert.Empty(await RowsAsync(account.UserId));
    }

    [Fact]
    public async Task Explicit_acceptance_is_idempotent_for_same_version_and_replays_an_empty_response()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        var key = Guid.NewGuid();
        using var accepted = await SendAsync(client, " 1 ", key);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Equal(string.Empty, await accepted.Content.ReadAsStringAsync(Ct));
        using var replay = await SendAsync(client, " 1 ", key);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        using var again = await SendAsync(client, "1", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal("1", Assert.Single(await RowsAsync(account.UserId)).NoticeVersion);
    }

    [Fact]
    public async Task Concurrent_explicit_acceptances_create_one_record()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(client, "1", Guid.NewGuid())));
        foreach (var response in responses)
            using (response) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(await RowsAsync(account.UserId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Acceptance_follows_the_user_across_personal_and_two_organizations(bool businessFirst)
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        var first = await OrganizationJourney.StageAsync(factory, account, "Notice first organization", Ct);
        var second = await OrganizationJourney.StageAsync(factory, account, "Notice second organization", Ct);
        await AssistantTestData.EnableAsync(factory, first);
        await AssistantTestData.EnableAsync(factory, second);
        if (businessFirst) await AccountJourney.AuthorizeAsync(client, "business", Ct, first);
        using (var accepted = await SendAsync(client, "1", Guid.NewGuid(), businessFirst ? "/api/organization/assistant/notice" : "/api/me/assistant/notice"))
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        var original = Assert.Single(await RowsAsync(account.UserId));
        foreach (var tenant in new[] { first, second })
        {
            await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
            using var accepted = await SendAsync(client, "1", Guid.NewGuid(), "/api/organization/assistant/notice");
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        }
        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        using (var accepted = await SendAsync(client, "1", Guid.NewGuid())) Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        var final = Assert.Single(await RowsAsync(account.UserId));
        Assert.Equal(original.Id, final.Id);
        Assert.Equal(original.AcceptedAtUtc, final.AcceptedAtUtc);
        Assert.Equal(businessFirst ? Access.Business : Access.Consumer, final.Access);
        using var other = AssistantTestData.Client(factory);
        var outsider = await AccountJourney.RegisterAsync(factory, other, Ct);
        Assert.Empty(await RowsAsync(outsider.UserId));
    }

    [Fact]
    public async Task New_trusted_notice_revision_rejects_old_success_replay_and_requires_a_new_explicit_action()
    {
        var revision = new NoticeRevisionProbe();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var original = services.Single(row => row.ServiceType == typeof(IAssistantNoticeReader));
            services.Replace(ServiceDescriptor.Scoped<IAssistantNoticeReader>(provider => new NoticeRevision(
                (IAssistantNoticeReader)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!), revision)));
        }));
        using var revised = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, revised, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        var oldKey = Guid.NewGuid();
        using (var response = await SendAsync(revised, "1", oldKey)) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        revision.Version = "2";
        using (var replay = await SendAsync(revised, "1", oldKey))
        {
            Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
            Assert.False(replay.Headers.Contains("Idempotent-Replayed"));
            using var problem = await replay.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal("Leé el aviso antes de tu primera consulta.", problem!.RootElement.GetProperty("detail").GetString());
            Assert.True(problem.RootElement.TryGetProperty("traceId", out _));
            Assert.Equal("Leé el aviso antes de tu primera consulta.", problem.RootElement.GetProperty("errors").GetProperty("noticeVersion")[0].GetString());
        }
        revised.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US");
        using (var replay = await SendAsync(revised, "1", oldKey))
        {
            using var problem = await replay.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal("Read the notice before your first query.", problem!.RootElement.GetProperty("detail").GetString());
        }
        Assert.Single(await RowsAsync(account.UserId));
        using (var response = await SendAsync(revised, "2", Guid.NewGuid())) Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(["1", "2"], (await RowsAsync(account.UserId)).Select(row => row.NoticeVersion).Order().ToArray());
    }

    [Fact]
    public async Task New_legal_revision_blocks_acceptance_and_success_replay_until_explicit_legal_consent()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        using var client = AssistantTestData.Client(isolated);
        var account = await AccountJourney.RegisterAsync(isolated, client, Ct);
        await AssistantTestData.PersonalAsync(isolated, true);
        var key = Guid.NewGuid();
        using (var accepted = await SendAsync(client, "1", key)) Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        using (var scope = isolated.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
            {
                var doc = LegalDocument.Create(LegalDocumentKind.Terms, 3, services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime);
                var context = services.GetRequiredService<ApplicationDbContext>();
                context.Add(doc);
                context.AddRange(LegalDocumentContent.Create(doc.Id, "es-AR", "Nueva versión de prueba."),
                    LegalDocumentContent.Create(doc.Id, "en-US", "New test revision."));
                return Task.FromResult(Result.Success());
            }, CommitPolicy.OnSuccess, Ct);
        }
        foreach (var attempt in new[] { key, Guid.NewGuid() })
        {
            using var blocked = await SendAsync(client, "1", attempt);
            Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
            using var problem = await blocked.Content.ReadFromJsonAsync<JsonDocument>(Ct);
            Assert.Equal(LegalErrors.AcceptanceRequiredCode, problem!.RootElement.GetProperty("code").GetString());
        }
        using var pending = await client.GetFromJsonAsync<JsonDocument>("/api/me", Ct);
        var documents = pending!.RootElement.GetProperty("pendingLegalDocuments").EnumerateArray()
            .Select(row => new { id = row.GetProperty("id").GetGuid(), version = row.GetProperty("version").GetInt32() }).ToArray();
        using (var consent = await AccountJourney.PostAsync(client, "/api/legal/accept", new { documents }, Ct))
            Assert.Equal(HttpStatusCode.NoContent, consent.StatusCode);
        using var replay = await SendAsync(client, "1", key);
        Assert.Equal(HttpStatusCode.NoContent, replay.StatusCode);
        Assert.Equal("true", Assert.Single(replay.Headers.GetValues("Idempotent-Replayed")));
        using var verify = isolated.Services.CreateScope();
        Assert.Single(await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantNoticeAcceptance>()
            .Where(row => row.OwnerUserId == account.UserId).ToArrayAsync(Ct));
    }

    [Fact]
    public async Task Gate_revocation_denies_an_old_success_and_operator_uses_no_active_tenant()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        var key = Guid.NewGuid();
        using (var accepted = await SendAsync(client, "1", key)) Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        await AssistantTestData.PersonalAsync(factory, false);
        using (var replay = await SendAsync(client, "1", key)) Assert.Equal(HttpStatusCode.NotFound, replay.StatusCode);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Assistant.Use], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var platformKey = Guid.NewGuid();
        using (var accepted = await SendAsync(client, "1", platformKey, "/api/platform/assistant/notice"))
            Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Single(await RowsAsync(account.UserId));
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Audit.Read], Ct);
        using var denied = await SendAsync(client, "1", platformKey, "/api/platform/assistant/notice");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private sealed class NoticeRevisionProbe { internal string Version { get; set; } = "1"; }
    private sealed record NoticeRevision(IAssistantNoticeReader Inner, NoticeRevisionProbe Revision) : IAssistantNoticeReader
    {
        public string CurrentVersion => Revision.Version;
        public Task<bool> ExistsAsync(Guid ownerUserId, string noticeVersion, CancellationToken ct) => Inner.ExistsAsync(ownerUserId, noticeVersion, ct);
    }

    [Fact]
    public async Task A_failed_write_rolls_back_consent_and_the_same_key_can_be_retried()
    {
        var fault = new WriteFault();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            var original = services.Single(row => row.ServiceType == typeof(IAssistantNoticeRepository));
            services.Replace(ServiceDescriptor.Scoped<IAssistantNoticeRepository>(provider => new FaultingNoticeRepository(
                (IAssistantNoticeRepository)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!), fault)));
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        var key = Guid.NewGuid();
        using (var failed = await SendAsync(client, "1", key)) Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Empty(await RowsAsync(account.UserId));
        fault.Fail = false;
        using var accepted = await SendAsync(client, "1", key);
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Single(await RowsAsync(account.UserId));
    }

    private sealed class WriteFault { internal bool Fail { get; set; } = true; }
    private sealed record FaultingNoticeRepository(IAssistantNoticeRepository Inner, WriteFault Fault) : IAssistantNoticeRepository
    {
        public Task LockAsync(Guid ownerUserId, CancellationToken ct) => Inner.LockAsync(ownerUserId, ct);
        public void Add(AssistantNoticeAcceptance acceptance)
        {
            Inner.Add(acceptance);
            if (Fault.Fail) throw new InvalidOperationException("Fixture failure after staging.");
        }
    }

    private async Task<AssistantNoticeAcceptance[]> RowsAsync(Guid user)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Set<AssistantNoticeAcceptance>()
            .AsNoTracking().Where(row => row.OwnerUserId == user).ToArrayAsync(Ct);
    }

    internal static async Task<HttpResponseMessage> SendAsync(HttpClient client, string version, Guid key, string route = "/api/me/assistant/notice")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, route) { Content = JsonContent.Create(new { noticeVersion = version }) };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return await client.SendAsync(request, Ct);
    }
}
