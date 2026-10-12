using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;

/// <summary>Host propio con PostgreSQL de tests, ingreso real y un solo reemplazo externo.</summary>
internal sealed class AssistantTurnTestSession : IAsyncDisposable
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(), new MoneyJsonConverter() },
    };
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;
    internal ApiFactory Factory { get; }
    internal AssistantTurnTestProvider Provider { get; }
    internal WebApplicationFactory<Program> Host { get; }
    internal HttpClient Client { get; }
    internal AccountJourney Account { get; private set; } = null!;
    internal Guid? TenantId { get; private set; }
    internal string Path { get; private set; } = "/api/me/assistant";
    internal string Access { get; private set; } = "consumer";

    private AssistantTurnTestSession(ApiFactory factory, AssistantTurnTestProvider provider,
        WebApplicationFactory<Program> host, HttpClient client)
    {
        Factory = factory;
        Provider = provider;
        Host = host;
        Client = client;
    }

    internal static async Task<AssistantTurnTestSession> CreateAsync(string access = "consumer", bool acceptNotice = true,
        bool workerEnabled = true, DbCommandInterceptor? databaseObserver = null)
    {
        var factory = new ApiFactory();
        var provider = new AssistantTurnTestProvider { DatabaseObserver = databaseObserver };
        WebApplicationFactory<Program>? host = null;
        HttpClient? client = null;
        try
        {
            await factory.InitializeAsync();
            host = CreateHost(factory, provider, workerEnabled);
            client = NewClient(host);
            var session = new AssistantTurnTestSession(factory, provider, host, client) { Access = access };
            await session.PrepareAsync(acceptNotice);
            return session;
        }
        catch
        {
            provider.Release();
            client?.Dispose();
            if (host is not null) await host.DisposeAsync();
            await factory.DisposeAsync();
            throw;
        }
    }

    internal WebApplicationFactory<Program> Replica() => CreateHost(Factory, Provider);

    internal async Task<HttpClient> LoginReplicaAsync(WebApplicationFactory<Program> replica)
    {
        // Cada host Testing firma sus propios tokens. Ingresar realmente, sin copiar el bearer del primero.
        using (var scope = Host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var consumed = await db.LoginCodes.Where(row => row.Destination == Account.Email.Value
                    && row.ConsumedAtUtc != null).ToArrayAsync(ct);
                var setupUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime.AddHours(-1);
                foreach (var row in consumed) db.Entry(row).Property(value => value.CreatedAtUtc).CurrentValue = setupUtc;
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
        }
        var client = NewClient(replica);
        try
        {
            using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = Account.Email.Value }, Ct);
            Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
            var code = await PickupCodeReader.ReadAsync(replica.Services, Account.Email.Value, Ct);
            using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
                new { email = Account.Email.Value, code, returnUrl = "/invitacion" }, Ct);
            Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
            await AccountJourney.AuthorizeAsync(client, Access, Ct, TenantId);
            return client;
        }
        catch { client.Dispose(); throw; }
    }

    private static WebApplicationFactory<Program> CreateHost(ApiFactory factory, AssistantTurnTestProvider provider, bool workerEnabled = true) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Assistant:Platform:ApiKey", "isolated-turn-provider-key");
            builder.UseSetting("Assistant:TurnWorkerEnabled", workerEnabled ? "true" : "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IChatClientFactory>();
                services.AddScoped<IChatClientFactory>(provider.Factory);
                services.AddSingleton<ILoggerProvider>(provider.Logs);
                if (provider.DatabaseObserver is { } observer)
                    services.AddDbContext<ApplicationDbContext>((_, options) => options.AddInterceptors(observer));
            });
        });

    internal static HttpClient NewClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task PrepareAsync(bool acceptNotice)
    {
        Account = Access == "platform" ? await PlatformAccountAsync(Host, Client)
            : await AccountJourney.RegisterAsync(Host, Client, Ct);
        using (var scope = Host.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var settings = await services.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformSettings>().SingleAsync(ct);
                settings.Update(settings.DefaultModelId, settings.AllowedModelIds, settings.Effort, 50,
                    new Money(1000m, CurrencyCode.Create("USD").Value), true, 30);
                services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(
                    AuditActorKind.System, Guid.Empty, "Assistant turn test configuration",
                    services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True(result.IsSuccess, result.Error.Code);
        }
        if (Access == "business")
        {
            TenantId = await OrganizationJourney.StageAsync(Factory, Account, "Turn organization", Ct);
            await AssistantTestData.EnableAsync(Factory, TenantId.Value);
            await AccountJourney.AuthorizeAsync(Client, Access, Ct, TenantId);
            Path = "/api/organization/assistant";
        }
        else if (Access == "platform")
        {
            await AccountJourney.AuthorizeAsync(Client, Access, Ct);
            Path = "/api/platform/assistant";
        }
        else
        {
            Assert.Equal("consumer", Access);
            TenantId = await AssistantTestData.ActiveTenantAsync(Client);
        }
        using var state = await Client.GetAsync(Path, Ct);
        Assert.Equal(HttpStatusCode.OK, state.StatusCode);
        using var value = JsonDocument.Parse(await state.Content.ReadAsStringAsync(Ct));
        Assert.True(value.RootElement.GetProperty("configured").GetBoolean());
        Assert.Equal(0, Provider.Calls);
        if (acceptNotice) await AssistantExecutionActorTests.AcceptNoticeAsync(Client, Path + "/notice");
    }

    internal static async Task<AccountJourney> PlatformAccountAsync(WebApplicationFactory<Program> host, HttpClient client)
    {
        await host.Services.SeedDatabaseAsync(Ct);
        var email = Email.Create("turn-operator-" + Guid.NewGuid().ToString("N") + "@example.test").Value;
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var created = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
        {
            var accounts = services.GetRequiredService<IUserRepository>();
            var user = await accounts.CreateAsync("Operador de turnos", "es-AR", "America/Argentina/Buenos_Aires", ct);
            var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
            await accounts.SetPlatformOperatorAsync(user.Id, true, ct);
            await accounts.SetPrimaryEmailAsync(user.Id, email, ct);
            var method = LoginMethod.CreateEmail(user.Id, email);
            method.Verify(nowUtc);
            Assert.True(method.MakePrimary().IsSuccess);
            services.GetRequiredService<ILoginMethodRepository>().Add(method);
            services.GetRequiredService<IPlatformPermissionAssignmentRepository>().Add(
                PlatformPermissionAssignment.Create(user.Id, PlatformOperators.All).Value);
            services.GetRequiredService<ISecurityEventRepository>().Add(
                SecurityEvent.ForPlatformOperatorGrant("Assistant exclusive operator fixture", nowUtc, user.Id));
            var legal = services.GetRequiredService<ILegalRepository>();
            foreach (var kind in Enum.GetValues<LegalDocumentKind>())
            {
                var document = await legal.GetCurrentDocumentAsync(kind, nowUtc, ct)
                    ?? throw new InvalidOperationException("Current legal documents required.");
                legal.AddAcceptance(LegalAcceptance.Create(user.Id, document, nowUtc, null, null));
            }
            return Result.Success(user.Id);
        }, CommitPolicy.OnSuccess, Ct);
        Assert.True(created.IsSuccess, created.Error.Code);
        Assert.Empty(await services.GetRequiredService<IUserTenantAccessReader>().ListForUserAsync(created.Value, Ct));
        using var requested = await AccountJourney.PostAsync(client, "/api/auth/login-code", new { email = email.Value }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);
        var code = await PickupCodeReader.ReadAsync(host.Services, email.Value, Ct);
        using var verified = await AccountJourney.PostAsync(client, "/api/auth/login-code/verify",
            new { email = email.Value, code, returnUrl = "/connect/authorize?access=platform" }, Ct);
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        return new(created.Value, email);
    }

    internal async Task<AssistantConversationSummaryResponse> ConversationAsync()
    {
        using var request = Request(HttpMethod.Post, Path + "/conversations", "{}", Guid.NewGuid());
        using var response = await Client.SendAsync(request, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AssistantConversationSummaryResponse>(Json, Ct))!;
    }

    internal Task<AssistantConversationResponse?> HistoryAsync(Guid conversationId, CancellationToken ct) =>
        Client.GetFromJsonAsync<AssistantConversationResponse>(Path + $"/conversations/{conversationId:D}", Json, ct);

    internal Task<HttpResponseMessage> OpenStreamAsync(HttpRequestMessage request, CancellationToken ct) =>
        Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).WaitAsync(TimeSpan.FromSeconds(20), ct);

    internal static string Body(AssistantConversationSummaryResponse conversation, string text = "Consulta privada de prueba") =>
        JsonSerializer.Serialize(new { text, attachmentIds = Array.Empty<Guid>(), conversationVersion = conversation.Version });

    internal static HttpRequestMessage Request(HttpMethod method, string path, string body, Guid key)
    {
        var request = new HttpRequestMessage(method, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("Idempotency-Key", key.ToString("D"));
        return request;
    }

    internal static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string? code = null)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        if (code is not null) Assert.Equal(code, problem.RootElement.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
    }

    public async ValueTask DisposeAsync()
    {
        Provider.Release();
        Client.Dispose();
        await Host.DisposeAsync();
        await Factory.DisposeAsync();
    }
}
