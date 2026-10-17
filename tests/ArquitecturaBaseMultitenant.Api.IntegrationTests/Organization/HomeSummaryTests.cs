using System.Data.Common;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Home;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Organization;

[Collection(ApiTestGroup.Name)]
public sealed class HomeSummaryTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_thousand_pending_items_have_real_totals_ten_rows_and_the_same_query_count_as_ten()
    {
        using var client = factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var owner = await AccountJourney.RegisterAsync(factory, client, Ct);
        var smallTenant = await OrganizationJourney.StageAsync(factory, owner, "Inicio diez", Ct);
        var largeTenant = await OrganizationJourney.StageAsync(factory, owner, "Inicio mil", Ct);
        await SeedAsync(smallTenant, owner.UserId, 10);
        await SeedAsync(largeTenant, owner.UserId, 1_000);

        var sql = new QueryProbe();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(sql));
            services.Replace(ServiceDescriptor.Scoped<ICurrentUser>(_ => new CurrentUser(owner.UserId)));
        }));
        var (small, smallQueries) = await ReadAsync(smallTenant);
        var (large, largeQueries) = await ReadAsync(largeTenant);
        var smallGroup = Assert.Single(small.PendingGroups!, group => group.Kind == "invitationExpired");
        var largeGroup = Assert.Single(large.PendingGroups!, group => group.Kind == "invitationExpired");
        Assert.Equal(10, smallGroup.TotalCount);
        Assert.Equal(1_000, largeGroup.TotalCount);
        Assert.Equal(10, smallGroup.Items.Count);
        Assert.Equal(10, largeGroup.Items.Count);
        Assert.Equal(smallQueries, largeQueries);
        Assert.InRange(smallQueries, 1, 50);
        Assert.All(large.PendingGroups!, group => Assert.InRange(group.Items.Count, 1, PendingGroup.PreviewLimit));

        // El consumidor interno conserva la agregación completa, sin depender de la vista previa HTTP.
        await using (var scope = host.Services.CreateAsyncScope())
        {
            scope.ServiceProvider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(largeTenant, TenantKind.Business);
            var complete = await scope.ServiceProvider.GetRequiredService<IHomeService>().GetAsync(Ct);
            Assert.True(complete.IsSuccess);
            Assert.Equal(1_000, complete.Value.Pending.Count(item => item.Kind == "invitationExpired"));
        }

        // La ruta pública del contrato de Inicio entrega la misma vista acotada y sus totales.
        await AccountJourney.AuthorizeAsync(client, "business", Ct, largeTenant);
        var response = await OrganizationJourney.HomeAsync(client, Ct);
        var invitationGroup = Assert.Single(response.GetProperty("pendingGroups").EnumerateArray(),
            group => group.GetProperty("kind").GetString() == "invitationExpired");
        Assert.Equal(10, invitationGroup.GetProperty("items").GetArrayLength());
        Assert.Equal(1_000, invitationGroup.GetProperty("totalCount").GetInt32());

        async Task<(HomeResponse Home, int Queries)> ReadAsync(Guid tenantId)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            services.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenantId, TenantKind.Business);
            var service = services.GetRequiredService<IHomeService>();
            sql.Target = services.GetRequiredService<ApplicationDbContext>();
            // Calienta el caché real de permisos; el recorrido medido es exactamente el mismo para ambos tamaños.
            Assert.True((await service.GetSummaryAsync(Ct)).IsSuccess);
            sql.Reset();
            var result = await service.GetSummaryAsync(Ct);
            Assert.True(result.IsSuccess);
            return (result.Value, sql.Count);
        }
    }

    private async Task SeedAsync(Guid tenantId, Guid inviterId, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        using var tenant = services.GetRequiredService<ITenantScope>().Enter(tenantId);
        var nowUtc = services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            for (var index = 0; index < count; index++)
            {
                var member = Member.Invite();
                context.Members.Add(member);
                context.Invitations.Add(Invitation.Issue(member.Id, inviterId,
                    Email.Create($"summary-{member.Id:N}@example.test").Value, "Email", member.Id.ToString("N"),
                    nowUtc.AddDays(-4).AddMinutes(index / 2), TimeSpan.FromDays(1)));
            }
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private sealed class CurrentUser(Guid userId) : ICurrentUser
    {
        public Guid? UserId => userId;
        public Access? Access => ArquitecturaBaseMultitenant.Domain.Users.Access.Business;
    }

    private sealed class QueryProbe : DbCommandInterceptor
    {
        public ApplicationDbContext? Target { get; set; }
        public int Count { get; private set; }
        public void Reset() => Count = 0;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (ReferenceEquals(eventData.Context, Target)) Count++;
            return ValueTask.FromResult(result);
        }
    }
}
