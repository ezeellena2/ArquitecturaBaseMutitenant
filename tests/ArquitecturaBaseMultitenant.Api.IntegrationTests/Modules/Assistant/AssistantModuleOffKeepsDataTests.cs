using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantTurns;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Retention;
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
/// Apagar no borra datos (ADR 0069): Plataforma apaga el Asistente de una organización con su <c>PUT</c>, sus rutas dan 404 y
/// sus conversaciones, su uso y su configuración siguen en la base; la purga de siempre borra solo lo que venció, y al prenderlo
/// otra vez se ve igual. Base propia: la purga recorre todas las organizaciones.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class AssistantModuleOffKeepsDataTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly DateTime NowUtc = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
    private const string BusinessPath = "/api/organization/assistant";
    private const string Usage = BusinessPath + "/usage?month=2026-10";
    private static Money Usd(decimal amount) => new(amount, CurrencyCode.Create("USD").Value);

    [Fact]
    public async Task Turning_it_off_hides_its_routes_keeps_its_rows_lets_the_usual_purge_run_and_turning_it_on_shows_them_the_same()
    {
        await using var factory = new ApiFactory();
        await factory.InitializeAsync();
        var provider = new AssistantTurnTestProvider();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(NowUtc)));
            services.RemoveAll<IChatClientFactory>();
            services.AddScoped<IChatClientFactory>(provider.Factory);
        }));
        using var client = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, client, Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, owner, "Assistant off keeps data", Ct);
        await AccountJourney.AuthorizeAsync(client, "business", Ct, tenant);
        using var platform = Client(host);
        var operatorAccount = await AccountJourney.RegisterAsync(host, platform, Ct);
        await PlatformOperators.GrantAsync(host.Services, operatorAccount.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);

        // Plataforma lo prende y la organización lo usa: configuración propia, dos conversaciones y el uso del mes.
        var version = await TurnAsync(platform, tenant, true, 0);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client, BusinessPath + "/notice");
        await OwnKeyAsync(host, tenant, 40, Usd(70));
        var kept = await ConversationAsync(host, tenant, owner.UserId, "Ventas de octubre", 0);
        var expired = await ConversationAsync(host, tenant, owner.UserId, null, await RetentionDaysAsync(host) + 1);
        await CostAsync(host, owner.UserId, await MonthAsync(host, tenant), 8m);
        var before = await ViewAsync(client, kept.Id);
        Assert.NotNull(Item(before.Conversations, expired.Id));
        var rows = await CountAsync(host, tenant);
        Assert.Equal(new Rows(2, 1, 1, 1, 1), rows);

        // Apagado: sus rutas dan 404 y sus filas siguen en la base.
        version = await TurnAsync(platform, tenant, false, version);
        foreach (var path in new[] { BusinessPath, BusinessPath + "/conversations", $"{BusinessPath}/conversations/{kept.Id:D}",
                     BusinessPath + "/settings", Usage })
        {
            using var hidden = await client.GetAsync(path, Ct);
            Assert.True(hidden.StatusCode == HttpStatusCode.NotFound, $"{path}: {(int)hidden.StatusCode}");
        }
        Assert.Equal(rows, await CountAsync(host, tenant));

        // La purga de siempre sigue con el módulo apagado: borra solo la conversación vencida, no el resto.
        await ActivatorUtilities.CreateInstance<AssistantRetentionWorker>(host.Services).RunOnceAsync(Ct);
        Assert.Equal(rows with { Conversations = 1 }, await CountAsync(host, tenant));
        Assert.Equal(0, provider.Calls);

        // Prendido otra vez, se ve igual.
        await TurnAsync(platform, tenant, true, version);
        var after = await ViewAsync(client, kept.Id);
        Assert.Equal(before.State, after.State);
        Assert.Equal(before.Detail, after.Detail);
        Assert.Equal(before.Settings, after.Settings);
        Assert.Equal(before.Usage, after.Usage);
        Assert.Equal(Item(before.Conversations, kept.Id), Item(after.Conversations, kept.Id));
        Assert.Null(Item(after.Conversations, expired.Id));
    }

    // El reloj del host está fijo: las cookies de sesión vencen con él, no con el real.
    private static HttpClient Client(WebApplicationFactory<Program> host) =>
        TestClockCookies.Client(host, new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    /// <summary>Plataforma prende o apaga el módulo con la pestaña «Módulos», con la versión que leyó.</summary>
    private static async Task<uint> TurnAsync(HttpClient platform, Guid tenant, bool enabled, uint version)
    {
        using var saved = await platform.PutAsJsonAsync($"/api/platform/tenants/{tenant:D}/features/{AssistantFeature.Key}",
            new { enabled, source = "Manual", version, reason = enabled ? "Prender el asistente" : "Apagar el asistente" }, Ct);
        return await SavedVersion.ReadAsync(saved, Ct);
    }

    private sealed record View(string State, JsonElement Conversations, string Detail, string Settings, string Usage);

    private static async Task<View> ViewAsync(HttpClient client, Guid conversationId) => new(
        await ReadAsync(client, BusinessPath),
        JsonDocument.Parse(await ReadAsync(client, BusinessPath + "/conversations")).RootElement.Clone(),
        await ReadAsync(client, $"{BusinessPath}/conversations/{conversationId:D}"),
        await ReadAsync(client, BusinessPath + "/settings"),
        await ReadAsync(client, Usage));

    private static async Task<string> ReadAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path}: {(int)response.StatusCode} {body}");
        return body;
    }

    /// <summary>El elemento de una lista con ese id, en texto, o null si no está.</summary>
    private static string? Item(JsonElement root, Guid id) => root.ValueKind switch
    {
        JsonValueKind.Object when root.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String
            && value.GetGuid() == id => root.GetRawText(),
        JsonValueKind.Object => root.EnumerateObject().Select(property => Item(property.Value, id)).FirstOrDefault(found => found is not null),
        JsonValueKind.Array => root.EnumerateArray().Select(element => Item(element, id)).FirstOrDefault(found => found is not null),
        _ => null,
    };

    private sealed record Rows(int Conversations, int Settings, int UsageFacts, int Operations, int Months);

    private static async Task<Rows> CountAsync(WebApplicationFactory<Program> host, Guid tenant)
    {
        await using var scope = host.Services.CreateAsyncScope();
        using var enter = scope.ServiceProvider.GetRequiredService<ITenantScope>().Enter(tenant);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new(await db.Set<AssistantConversation>().CountAsync(Ct),
            await db.Set<AssistantOrganizationSettings>().CountAsync(Ct),
            await db.Set<AssistantUsageFact>().CountAsync(Ct),
            await db.Set<AssistantFinancialOperation>().CountAsync(row => row.ScopeTenantId == tenant, Ct),
            await db.Set<AssistantBudgetMonth>().CountAsync(row => row.OrganizationId == tenant, Ct));
    }

    private static async Task<int> RetentionDaysAsync(WebApplicationFactory<Program> host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAssistantRetentionRepository>().RetentionDaysAsync(Ct);
    }

    /// <summary>Una conversación de la organización con la última actividad de hace <paramref name="ageDays"/> días.</summary>
    private static async Task<AssistantConversation> ConversationAsync(WebApplicationFactory<Program> host, Guid tenant, Guid owner,
        string? title, int ageDays)
    {
        var conversation = AssistantConversation.Create(owner, Access.Business, NowUtc.AddDays(-ageDays));
        await WriteAsync(host, tenant, services =>
        {
            if (title is not null) conversation.Update(title, false, NowUtc.AddDays(-ageDays));
            services.GetRequiredService<ApplicationDbContext>().Add(conversation);
            return Task.FromResult(Result.Success());
        });
        return conversation;
    }

    private static Task OwnKeyAsync(WebApplicationFactory<Program> host, Guid tenant, int dailyLimit, Money budget) =>
        WriteAsync(host, tenant, async services =>
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var platform = await db.Set<AssistantPlatformSettings>().SingleAsync(Ct);
            var settings = AssistantOrganizationSettings.Create();
            db.Add(settings);
            settings.Update(nameof(AssistantMode.OwnKey), platform.DefaultModelId, platform.Effort, dailyLimit, budget, 30);
            settings.ReplaceKey(services.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector(AssistantKeyProtector.OrganizationPurpose, tenant.ToString("N"))
                .Protect("isolated-module-off-key"u8.ToArray()), "test");
            var policy = await db.Set<AssistantOrganizationPolicy>().SingleOrDefaultAsync(row => row.OrganizationId == tenant, Ct);
            if (policy is null) { policy = AssistantOrganizationPolicy.Create(tenant); db.Add(policy); }
            policy.SetAllowOwnKey(true);
            services.GetRequiredService<ISecurityEventRepository>().Add(SecurityEvent.ForTenantFeatures(
                AuditActorKind.System, Guid.Empty, tenant, "Module off own key fixture", NowUtc));
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
            var price = AssistantPriceSnapshot.Create(Guid.CreateVersion7(), "module-off-fixture", "historical-v1",
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

    private static async Task WriteAsync(WebApplicationFactory<Program> host, Guid tenant, Func<IServiceProvider, Task<Result>> work)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        using var tenantScope = services.GetRequiredService<ITenantScope>().Enter(tenant);
        var result = await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ => work(services), CommitPolicy.OnSuccess, Ct);
        Assert.True(result.IsSuccess, result.Error.Code);
    }
}
