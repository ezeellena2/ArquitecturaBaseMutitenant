using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Los topes de la IA con la clave de la plataforma pasan por el resolvedor (ADR 0069 §4, B4): dan lo mismo que antes, bajar
/// el máximo de Plataforma baja el cupo del día y el presupuesto del mes de una organización, y con clave propia valen los suyos.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantModuleLimitTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    // 09:00 en Buenos Aires: el día del cupo es el 10 y vuelve a las 03:00 UTC del 11.
    private static readonly DateTime NowUtc = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private const string BusinessPath = "/api/organization/assistant";
    private static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);

    [Fact]
    public async Task With_the_platform_key_the_organization_the_person_and_Platform_read_the_platform_maximum_as_before()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var provider = new AssistantTurnTestProvider();
        using var host = Host(factory, provider);
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await BusinessAsync(factory, client, account);
        await PlatformAsync(host, 50, Usd(100));

        using (var state = await ReadAsync(client, BusinessPath))
        {
            Assert.Equal(50, state.RootElement.GetProperty("dailyLimit").GetInt32());
            AssertMoney(state.RootElement, "monthlyBudget", 100);
        }
        using (var settings = await ReadAsync(client, BusinessPath + "/settings"))
        {
            Assert.Equal(nameof(AssistantMode.PlatformKey), settings.RootElement.GetProperty("mode").GetString());
            Assert.Equal(50, settings.RootElement.GetProperty("dailyMessageLimit").GetInt32());
            AssertMoney(settings.RootElement, "monthlyBudget", 100);
        }

        await AccountJourney.AuthorizeAsync(client, "consumer", Ct);
        using (var personal = await ReadAsync(client, "/api/me/assistant"))
        {
            Assert.Equal(50, personal.RootElement.GetProperty("dailyLimit").GetInt32());
            Assert.Equal(JsonValueKind.Null, personal.RootElement.GetProperty("monthlyBudget").ValueKind);
        }

        using var operatorClient = Client(host);
        var operatorAccount = await AccountJourney.RegisterAsync(host, operatorClient, Ct);
        await PlatformOperators.GrantAsync(host.Services, operatorAccount.UserId, [PlatformPermissions.Assistant.Use], Ct);
        await AccountJourney.AuthorizeAsync(operatorClient, "platform", Ct);
        using (var platform = await ReadAsync(operatorClient, "/api/platform/assistant"))
        {
            Assert.Equal(50, platform.RootElement.GetProperty("dailyLimit").GetInt32());
            Assert.Equal(JsonValueKind.Null, platform.RootElement.GetProperty("monthlyBudget").ValueKind);
        }
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task Lowering_the_platform_maximum_lowers_the_daily_quota_and_the_monthly_budget_of_an_organization_before_the_provider()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var provider = new AssistantTurnTestProvider();
        using var host = Host(factory, provider);
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await BusinessAsync(factory, client, account);
        await PlatformAsync(host, 50, Usd(100));
        await WriteAsync(host, null, services =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(Quota(account.UserId, tenant, new(2026, 10, 10), 3));
            return Task.FromResult(Result.Success());
        });
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, BusinessPath + "/notice");

        // Con 3 mensajes usados de 50 entraría; con el máximo en 3, el 429 llega antes y sin llamar al proveedor.
        await PlatformAsync(host, 3, Usd(100));
        using (var state = await ReadAsync(client, BusinessPath))
        {
            Assert.Equal(3, state.RootElement.GetProperty("dailyLimit").GetInt32());
            Assert.Equal(0, state.RootElement.GetProperty("remainingMessages").GetInt32());
        }
        using (var limited = await SendAsync(client))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            using var problem = JsonDocument.Parse(await limited.Content.ReadAsStringAsync(Ct));
            Assert.Equal(AssistantErrors.LimitDailyReachedCode, problem.RootElement.GetProperty("code").GetString());
            Assert.Equal(3, problem.RootElement.GetProperty("dailyLimit").GetInt32());
        }

        // Sin un mes abierto, el estado y la configuración muestran el presupuesto nuevo.
        await PlatformAsync(host, 50, Usd(5));
        using (var state = await ReadAsync(client, BusinessPath))
        {
            Assert.Equal(50, state.RootElement.GetProperty("dailyLimit").GetInt32());
            AssertMoney(state.RootElement, "monthlyBudget", 5);
        }
        using (var settings = await ReadAsync(client, BusinessPath + "/settings"))
        {
            Assert.Equal(50, settings.RootElement.GetProperty("dailyMessageLimit").GetInt32());
            AssertMoney(settings.RootElement, "monthlyBudget", 5);
        }
        // Con 8 USD gastados de 100 entraría; con el presupuesto en 5 USD, la organización ya llegó al tope del mes.
        var month = await MonthAsync(host, tenant);
        await CostAsync(host, account.UserId, month, 8m);
        using (var limited = await SendAsync(client))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
            using var problem = JsonDocument.Parse(await limited.Content.ReadAsStringAsync(Ct));
            Assert.Equal(AssistantErrors.LimitMonthlyReachedCode, problem.RootElement.GetProperty("code").GetString());
        }
        Assert.Equal(0, provider.Calls);
    }

    [Fact]
    public async Task With_its_own_key_an_organization_keeps_its_limits_when_the_platform_maximum_drops()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var provider = new AssistantTurnTestProvider();
        using var host = Host(factory, provider);
        using var client = Client(host);
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await BusinessAsync(factory, client, account);
        await PlatformAsync(host, 50, Usd(100));
        await OwnKeyAsync(host, tenant, 40, Usd(70));

        await PlatformAsync(host, 3, Usd(5));
        using (var state = await ReadAsync(client, BusinessPath))
        {
            Assert.Equal(40, state.RootElement.GetProperty("dailyLimit").GetInt32());
            AssertMoney(state.RootElement, "monthlyBudget", 70);
        }
        using (var settings = await ReadAsync(client, BusinessPath + "/settings"))
        {
            Assert.Equal(nameof(AssistantMode.OwnKey), settings.RootElement.GetProperty("mode").GetString());
            Assert.Equal(40, settings.RootElement.GetProperty("dailyMessageLimit").GetInt32());
            AssertMoney(settings.RootElement, "monthlyBudget", 70);
        }
        Assert.Equal(0, provider.Calls);
    }

    private static WebApplicationFactory<Program> Host(ApiFactory factory, AssistantTurnTestProvider provider) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(NowUtc)));
            services.RemoveAll<IChatClientFactory>();
            services.AddScoped<IChatClientFactory>(provider.Factory);
        }));

    // El reloj del host está fijo: las cookies de sesión vencen con él, no con el real.
    private static HttpClient Client(WebApplicationFactory<Program> host) =>
        TestClockCookies.Client(host, new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static async Task<Guid> BusinessAsync(ApiFactory factory, HttpClient client, AccountJourney account)
    {
        var tenant = await OrganizationJourney.StageAsync(factory, account, "Module limit organization", Ct);
        await AssistantTestData.EnableAsync(factory, tenant);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        return tenant;
    }

    private static byte[] Cipher(IServiceProvider services, string purpose, params string[] scope) =>
        services.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose, scope)
            .Protect("isolated-module-limit-key"u8.ToArray());

    private static Task PlatformAsync(WebApplicationFactory<Program> host, int dailyLimit, Money budget) =>
        WriteAsync(host, null, async services =>
        {
            var settings = await services.GetRequiredService<ApplicationDbContext>().Set<AssistantPlatformSettings>().SingleAsync(Ct);
            settings.Update(settings.DefaultModelId, settings.AllowedModelIds, settings.Effort, dailyLimit, budget, true, 30);
            settings.ReplaceKey(Cipher(services, AssistantKeyProtector.PlatformPurpose), "test");
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForPlatformSettings(
                AuditActorKind.System, Guid.Empty, "Module limit fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        });

    private static Task OwnKeyAsync(WebApplicationFactory<Program> host, Guid tenant, int dailyLimit, Money budget) =>
        WriteAsync(host, tenant, async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var platform = await db.Set<AssistantPlatformSettings>().SingleAsync(Ct);
            var settings = await db.Set<AssistantOrganizationSettings>().SingleOrDefaultAsync(Ct);
            if (settings is null) { settings = AssistantOrganizationSettings.Create(); db.Add(settings); }
            settings.Update(nameof(AssistantMode.OwnKey), platform.DefaultModelId, platform.Effort, dailyLimit, budget, 30);
            settings.ReplaceKey(Cipher(services, AssistantKeyProtector.OrganizationPurpose, tenant.ToString("N")), "test");
            var policy = await db.Set<AssistantOrganizationPolicy>().SingleOrDefaultAsync(row => row.OrganizationId == tenant, Ct);
            if (policy is null) { policy = AssistantOrganizationPolicy.Create(tenant); db.Add(policy); }
            policy.SetAllowOwnKey(true);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(
                AuditActorKind.System, Guid.Empty, tenant, "Module limit own funding fixture", services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime));
            return Result.Success();
        });

    private static async Task<AssistantBudgetMonth> MonthAsync(WebApplicationFactory<Program> host, Guid tenant)
    {
        var month = AssistantBudgetMonth.Create(tenant, new(2026, 10, 1), "America/Argentina/Buenos_Aires",
            new(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc), new(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc));
        await WriteAsync(host, tenant, services =>
        {
            services.GetRequiredService<ApplicationDbContext>().Add(month);
            return Task.FromResult(Result.Success());
        });
        return month;
    }

    // Un gasto ya cerrado del mes con la clave de la plataforma, como lo deja un turno terminado.
    private static Task CostAsync(WebApplicationFactory<Program> host, Guid owner, AssistantBudgetMonth month, decimal cost) =>
        WriteAsync(host, month.OrganizationId, services =>
        {
            var price = AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "module-limit-fixture", "historical-v1",
                Usd(cost * 1000), Usd(0), Usd(0), Usd(0), Usd(0), null, null, null, null, null, null);
            var operation = AssistantFinancialOperation.Create(Guid.CreateVersion7(), new(owner, Access.Business, month.OrganizationId),
                AssistantKeyFunding.PlatformKey, DateOnly.FromDateTime(NowUtc), 50, month.Id, Usd(100), Usd(25), price, null, NowUtc.AddMinutes(-10));
            var db = services.GetRequiredService<ApplicationDbContext>();
            db.Add(operation);
            operation.MarkInvoked();
            var call = AssistantFinancialCall.Create(operation.Id, 1, false, price, 1000, 0, Usd(cost));
            call.ReplaceUsage(1000, 0, 0, 0, 0, 0, null, false, Usd(cost));
            db.Add(call);
            call.FinalizeUsage();
            operation.Complete(NowUtc.AddMinutes(-9));
            db.Add(AssistantUsageFact.Create(operation, call));
            return Task.FromResult(Result.Success());
        });

    private static AssistantPlatformKeyDailyQuota Quota(Guid userId, Guid organizationId, DateOnly day, int consumed)
    {
        var quota = AssistantPlatformKeyDailyQuota.Create(userId, organizationId, day);
        for (var index = 0; index < consumed; index++) { quota.Reserve(); quota.Consume(); }
        return quota;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client)
    {
        using var create = AssistantTurnTestSession.Request(HttpMethod.Post, BusinessPath + "/conversations", "{}", Guid.NewGuid());
        using var created = await client.SendAsync(create, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var conversation = JsonSerializer.Deserialize<AssistantConversationSummaryResponse>(
            await created.Content.ReadAsStringAsync(Ct), AssistantTurnTestSession.Json)!;
        using var send = AssistantTurnTestSession.Request(HttpMethod.Post, BusinessPath + $"/conversations/{conversation.Id:D}/messages",
            AssistantTurnTestSession.Body(conversation), Guid.NewGuid());
        return await client.SendAsync(send, Ct);
    }

    private static async Task WriteAsync(WebApplicationFactory<Program> host, Guid? tenant, Func<IServiceProvider, Task<Result>> work)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenantScope = tenant is { } id ? services.GetRequiredService<ITenantScope>().Enter(id) : null;
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => work(services), CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.Error.Code);
    }

    private static async Task<JsonDocument> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
    }

    private static void AssertMoney(JsonElement state, string name, decimal amount)
    {
        var money = state.GetProperty(name);
        Assert.Equal(amount, money.GetProperty("amount").GetDecimal());
        Assert.Equal("USD", money.GetProperty("currency").GetString());
    }
}
