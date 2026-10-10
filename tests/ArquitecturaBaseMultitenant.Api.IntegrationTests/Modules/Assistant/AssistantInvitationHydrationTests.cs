using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Pagination;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Models.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantInvitationHydrationTests(ApiFactory factory)
{
    [Theory]
    [InlineData("Pending", true, true)]
    [InlineData("Pending", true, false)]
    [InlineData("Pending", false, true)]
    [InlineData("Expired", true, true)]
    [InlineData("Expired", true, false)]
    [InlineData("Expired", false, true)]
    public async Task Incomplete_hydration_returns_controlled_unavailability_without_partial_rows(
        string status, bool missingId, bool missingExpiry)
    {
        var reader = new HydratedPage(status, missingId, missingExpiry);
        await WithToolAsync(reader, async tool =>
        {
            var result = await tool.ExecuteAsync(Args(status, 1), AssistantTestData.Ct);
            Assert.True(result.IsFailure);
            Assert.Equal(AssistantErrors.QuickQueryNotAvailable.Code, result.Error.Code);
            Assert.Equal(1, reader.Calls);
        });
    }

    [Theory]
    [InlineData("Pending", 1, 20, true)]
    [InlineData("Pending", 2, 1, false)]
    [InlineData("Pending", 3, 0, false)]
    [InlineData("Expired", 1, 20, true)]
    [InlineData("Expired", 2, 1, false)]
    [InlineData("Expired", 3, 0, false)]
    public async Task Complete_and_out_of_range_pages_preserve_source_pagination(
        string status, int page, int count, bool more)
    {
        var reader = new HydratedPage(status, false, false);
        await WithToolAsync(reader, async tool =>
        {
            var result = await tool.ExecuteAsync(Args(status, page), AssistantTestData.Ct);
            Assert.True(result.IsSuccess);
            var data = Assert.IsType<AssistantInvitationsQuickDataResponse>(Assert.IsType<AssistantQuickToolData>(result.Value).Data);
            Assert.Equal(count, data.Invitations.Count);
            Assert.Equal(21, data.TotalCount);
            Assert.Equal(more, data.HasMore);
            Assert.All(data.Invitations, row =>
            {
                Assert.Contains(row.Id, reader.InvitationIds);
                Assert.Equal(reader.ExpiresAtUtc, row.ExpiresAtUtc);
                Assert.Equal(status, row.Status);
            });
            Assert.Equal(1, reader.Calls);
        });
    }

    private async Task WithToolAsync(HydratedPage reader, Func<IAssistantTool, Task> verify)
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, AssistantTestData.Ct);
        var tenant = await OrganizationJourney.StageAsync(factory, account, "Invitation hydration", AssistantTestData.Ct);
        await AssistantTestData.EnableAsync(factory, tenant);
        var counter = new NoChat();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new Actor(account.UserId)));
            services.Replace(ServiceDescriptor.Singleton<IOrganizationUserReader>(reader));
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
        }));
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Business);
        await verify(services.GetRequiredService<IEnumerable<IAssistantTool>>().Single(tool => tool.Key == "org_invitations"));
        Assert.Equal(0, counter.Calls);
    }

    private static JsonElement Args(string status, int page) => JsonSerializer.SerializeToElement(new { status, page, pageSize = 20 });
    private sealed record Actor(Guid Id) : ICurrentUser { public Guid? UserId => Id; public Access? Access => Domain.Users.Access.Business; }
    private sealed class HydratedPage(string status, bool missingId, bool missingExpiry) : IOrganizationUserReader
    {
        internal Guid[] InvitationIds { get; } = [.. Enumerable.Range(0, 21).Select(_ => Guid.NewGuid())];
        internal DateTime ExpiresAtUtc { get; } = new(2026, 10, 10, 0, 0, 0, DateTimeKind.Utc);
        internal int Calls { get; private set; }
        public Task<PagedResult<OrganizationUserRow>> ListAsync(ListOrganizationUsersRequest request, DateTime nowUtc, CancellationToken ct)
        {
            Calls++;
            var state = status == "Pending" ? OrganizationUserState.Invited : OrganizationUserState.Expired;
            Assert.Equal(state, request.State);
            Assert.Equal(20, request.PageSize);
            var rows = request.Page switch
            {
                1 => InvitationIds[..20],
                2 => InvitationIds[20..],
                _ => [],
            };
            return Task.FromResult(new PagedResult<OrganizationUserRow>([.. rows.Select((id, index) =>
                new OrganizationUserRow(Guid.NewGuid(), null, "Hydrated invitation", null, true, state,
                    index == 0 && missingId ? null : id, nowUtc,
                    index == 0 && missingExpiry ? null : ExpiresAtUtc, [], [], 1, nowUtc, null))], request.Page, request.PageSize, 21));
        }
        public Task<OrganizationUserFilterCountsResponse> GetFilterCountsAsync(ListOrganizationUsersRequest request, DateTime nowUtc, CancellationToken ct) => throw new InvalidOperationException("Only the typed page is requested.");
        public Task<OrganizationUserRow?> FindByIdAsync(Guid memberId, DateTime nowUtc, CancellationToken ct) => throw new InvalidOperationException("No invitation reread or automatic retry.");
    }
    private sealed class NoChat : IChatClientFactory
    {
        internal int Calls { get; private set; }
        public Task<Result<IChatClient>> CreateAsync(CancellationToken ct) { Calls++; throw new InvalidOperationException("Direct tool must not call AI."); }
        public Task<Result> ValidateKeyAsync(string key, CancellationToken ct) { Calls++; throw new InvalidOperationException("Direct tool must not call AI."); }
    }
}
