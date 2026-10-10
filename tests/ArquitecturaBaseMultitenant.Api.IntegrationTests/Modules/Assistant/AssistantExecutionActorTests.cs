using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.BusinessAccess;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantExecutionActorTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task Captured_personal_actor_uses_real_current_user_profile_tools_RLS_and_restores_empty_identity()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var one = await OrganizationJourney.StageAsync(factory, account, "Actor personal first", Ct);
        var two = await OrganizationJourney.StageAsync(factory, account, "Actor personal second", Ct);
        using var outsiderClient = AssistantTestData.Client(factory);
        var outsider = await AccountJourney.RegisterAsync(factory, outsiderClient, Ct);
        var foreign = await OrganizationJourney.StageAsync(factory, outsider, "Actor personal foreign", Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AcceptNoticeAsync(client);
        var actor = await CaptureAsync(client);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        Assert.Null(services.GetRequiredService<IHttpContextAccessor>().HttpContext);
        var prepared = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        Assert.True(prepared.IsSuccess, prepared.Error.Code);
        using (services.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value))
        {
            var current = services.GetRequiredService<ICurrentUser>();
            Assert.Equal(account.UserId, current.UserId);
            Assert.Equal((AuditActorKind.User, (Guid?)account.UserId), AuditActorResolver.Resolve(current));
            Assert.Equal(actor.TenantKind, services.GetRequiredService<ITenantContext>().TenantKind);
            var tools = services.GetRequiredService<IEnumerable<IAssistantTool>>().ToDictionary(tool => tool.Key);
            var organizations = await tools["my_organizations"].ExecuteAsync(Args(), Ct);
            Assert.True(organizations.IsSuccess, organizations.Error.Code);
            var rows = Assert.IsType<AssistantOrganizationsQuickDataResponse>(Assert.IsType<AssistantQuickToolData>(organizations.Value).Data).Organizations;
            Assert.Equal(new[] { one, two }.Order(), rows.Select(row => row.Id).Order());
            Assert.DoesNotContain(rows, row => row.Id == foreign);
            var signin = await tools["my_account"].ExecuteAsync(Args(), Ct);
            Assert.True(signin.IsSuccess, signin.Error.Code);
            var safe = Assert.IsType<AssistantAccountQuickDataResponse>(Assert.IsType<AssistantQuickToolData>(signin.Value).Data);
            Assert.DoesNotContain(safe.LoginMethods, method => method.MaskedValue.Contains(account.Email.Value, StringComparison.Ordinal));
        }
        Assert.Null(services.GetRequiredService<ICurrentUser>().UserId);
        Assert.Null(services.GetRequiredService<ITenantContext>().TenantId);
        Assert.Equal((AuditActorKind.System, (Guid?)null), AuditActorResolver.Resolve(services.GetRequiredService<ICurrentUser>()));
        Assert.True((await services.GetRequiredService<IExecutionActorFactory>().CaptureAsync(Ct)).IsFailure);
        var foreignBinding = actor with { TenantId = foreign, TenantKind = Domain.Tenancy.TenantKind.Business, Access = Domain.Users.Access.Business };
        Assert.True((await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(foreignBinding, Ct)).IsFailure);
    }

    [Fact]
    public async Task Execution_requires_the_current_notice_even_when_an_actor_was_captured_earlier()
    {
        using var client = AssistantTestData.Client(factory);
        await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        var actor = await CaptureAsync(client);
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var prepared = await services.GetRequiredService<IExecutionActorFactory>().RevalidateAsync(actor, Ct);
        Assert.True(prepared.IsSuccess);
        using (services.GetRequiredService<IExecutionActorScope>().Enter(prepared.Value))
        {
            var tool = services.GetRequiredService<IEnumerable<IAssistantTool>>().Single(tool => tool.Key == "my_account");
            Assert.True((await tool.ExecuteAsync(Args(), Ct)).IsFailure);
            await AcceptNoticeAsync(client);
            Assert.True((await tool.ExecuteAsync(Args(), Ct)).IsSuccess);
            using var removal = factory.Services.CreateScope();
            var provider = removal.ServiceProvider;
            await provider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var db = provider.GetRequiredService<ApplicationDbContext>();
                db.RemoveRange(await db.Set<AssistantNoticeAcceptance>().Where(row => row.OwnerUserId == actor.OwnerUserId).ToArrayAsync(ct));
                return Domain.Results.Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
            Assert.True((await tool.ExecuteAsync(Args(), Ct)).IsFailure);
        }
    }

    internal static async Task<ExecutionActor> CaptureAsync(HttpClient client)
    {
        var capture = await client.GetFromJsonAsync<ExecutionActorCapture>("_test/execution-actor", Json, Ct);
        Assert.NotNull(capture);
        Assert.True(capture.HttpScopeRejected);
        Assert.True(capture.KindlessAccessDenied);
        return capture.Actor;
    }

    internal static async Task AcceptNoticeAsync(HttpClient client, string route = "/api/me/assistant/notice")
    {
        using var result = await AccountJourney.PostAsync(client, route, new { noticeVersion = "1" }, Ct);
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
    }

    internal static async Task AcceptNoticeAsync(IServiceProvider services)
    {
        var accepted = await services.GetRequiredService<Application.Modules.Assistant.Interfaces.Services.IAssistantConversationService>()
            .AcceptNoticeAsync(new AssistantNoticeRequest("1"), Ct);
        Assert.True(accepted.IsSuccess, accepted.Error.Code);
    }

    private static JsonElement Args() => JsonSerializer.SerializeToElement(new { });
}
