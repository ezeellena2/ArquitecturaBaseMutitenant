using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Platform;

/// <summary>Opciones globales de organización de Auditoría, con sus filtros cruzados y permiso propio.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class PlatformAuditOrganizationsTests(ApiFactory factory)
{
    private const string Path = "/api/platform/audit/organizations";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Growing_options_are_paged_searchable_and_the_legacy_array_is_bounded()
    {
        var (platform, actorId) = await OperatorAsync([PlatformPermissions.Audit.Read]);
        using var client = platform;
        var tag = "paged-" + Guid.NewGuid().ToString("N");
        var tenants = Enumerable.Range(1, 25).Select(index => Business($"{tag} {index:000}")).ToArray();
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await SaveAsync(tenants, tenants.Select(tenant => SecurityEvent.ForTenantAdministration(
            SecurityEventType.OrganizationSuspended, actorId, tenant.Id, tag, nowUtc)));
        var path = Path + "/paged?period=all&auditSearch=" + tag;
        var first = await GetAsync(client, path);
        Assert.Equal(10, first.GetProperty("items").GetArrayLength());
        Assert.Equal(25, first.GetProperty("totalCount").GetInt32());
        Assert.True(first.GetProperty("hasNext").GetBoolean());
        var second = await GetAsync(client, path + "&page=2");
        Assert.Equal(10, second.GetProperty("items").GetArrayLength());
        Assert.Empty(first.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())
            .Intersect(second.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid())));
        var searched = await GetAsync(client, path + "&search=025");
        Assert.Equal(tenants[^1].Id, Assert.Single(searched.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(1, searched.GetProperty("totalCount").GetInt32());
        var legacy = await GetAsync(client, Path + "?period=all&search=" + tag);
        Assert.Equal(10, legacy.GetArrayLength());
    }

    [Fact]
    public async Task Options_are_distinct_current_names_beyond_the_page_and_ignore_their_own_filter()
    {
        var (platform, actorId) = await OperatorAsync([PlatformPermissions.Audit.Read]);
        using var client = platform;
        var tag = "opciones-" + Guid.NewGuid().ToString("N");
        var tenants = new[] { Business("Zapata " + tag), Business("Álvarez " + tag), Business("Álvarez " + tag) };
        var absent = Business("Sin eventos " + tag);
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await SaveAsync([.. tenants, absent], tenants.SelectMany(tenant => new[]
        {
            SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationSuspended, actorId,
                tenant.Id, tag, nowUtc.AddMinutes(-2)),
            SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationReactivated, actorId,
                tenant.Id, tag, nowUtc.AddMinutes(-1)),
        }));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var tenant = await services.GetRequiredService<ITenantRepository>().GetByIdAsync(tenants[0].Id, ct);
                Assert.NotNull(tenant);
                Assert.True(tenant.Rename("Ñandú " + tag).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        var page = await GetAsync(client, "/api/platform/audit?search=" + tag + "&limit=1");
        Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.True(page.GetProperty("hasMore").GetBoolean());
        var suffix = $"?search={tag}&limit=1&after={Uri.EscapeDataString(page.GetProperty("nextCursor").GetString()!)}"
            + $"&tenantId={absent.Id:D}";
        var options = (await GetAsync(client, Path + suffix)).EnumerateArray().ToArray();
        Assert.Equal(3, options.Length);
        Assert.Equal(3, options.Select(item => item.GetProperty("id").GetGuid()).Distinct().Count());
        Assert.Equal(tenants.Skip(1).OrderBy(tenant => tenant.Id).Select(tenant => tenant.Id).Append(tenants[0].Id),
            options.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal(["Álvarez " + tag, "Álvarez " + tag, "Ñandú " + tag],
            options.Select(item => item.GetProperty("name").GetString()).ToArray());
        var ignoredCursor = await GetAsync(client, Path + $"?search={tag}&after=bad&limit=1");
        Assert.Equal(options.Select(item => item.GetProperty("id").GetGuid()),
            ignoredCursor.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()));
        var renamed = await GetAsync(client, Path + "?search=" + Uri.EscapeDataString("Nandu " + tag));
        Assert.Equal(tenants[0].Id, Assert.Single(renamed.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Cross_filters_match_audit_events_and_sign_ins_never_add_organizations()
    {
        var (platform, actorId) = await OperatorAsync([PlatformPermissions.Audit.Read]);
        using var client = platform;
        using var person = Client();
        var affected = await AccountJourney.RegisterAsync(factory, person, Ct);
        var (otherOperator, otherActorId) = await OperatorAsync([PlatformPermissions.Audit.Read]);
        using var otherClient = otherOperator;
        var tag = "cruzados-" + Guid.NewGuid().ToString("N");
        var actorName = "Operadora " + tag;
        var tenant = Business("Destino " + tag);
        var other = Business("Otra " + tag);
        var nowUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        await SaveAsync([tenant, other],
        [
            SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationSuspended, actorId,
                tenant.Id, "Motivo " + tag, nowUtc),
            SecurityEvent.ForOrganizationMembershipEnded(actorId, tenant.Id, affected.UserId, nowUtc),
            SecurityEvent.ForTenantAdministration(SecurityEventType.OrganizationSuspended, otherActorId,
                other.Id, "Ajeno " + tag, nowUtc),
            SecurityEvent.ForAccountAdministration(SecurityEventType.AccountSessionsRevoked, actorId,
                affected.UserId, "Motivo " + tag, nowUtc),
        ]);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var user = await services.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(row => row.Id == actorId, ct);
                Assert.True(user.Rename(actorName).IsSuccess);
                var target = await services.GetRequiredService<ApplicationDbContext>().Users.SingleAsync(row => row.Id == affected.UserId, ct);
                Assert.True(target.Rename("Cuenta " + tag).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }

        foreach (var search in new[] { "Motivo " + tag, "Destino " + tag, actorName, "Cuenta " + tag })
        {
            var filters = "?type=organizations&search=" + Uri.EscapeDataString(search);
            var events = await GetAsync(client, "/api/platform/audit" + filters);
            var expected = events.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("targetTenantId").GetGuid()).Distinct().ToArray();
            Assert.Equal([tenant.Id], expected);
            var options = await GetAsync(client, Path + filters);
            Assert.Equal(expected, options.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray());
        }
        var byActor = await GetAsync(client, Path + $"?type=organizations&accountId={actorId:D}");
        Assert.Equal(tenant.Id, Assert.Single(byActor.EnumerateArray()).GetProperty("id").GetGuid());
        var byTarget = await GetAsync(client, Path + $"?type=organizations&accountId={affected.UserId:D}");
        Assert.Equal(tenant.Id, Assert.Single(byTarget.EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Empty((await GetAsync(client, Path + $"?type=accounts&accountId={affected.UserId:D}")).EnumerateArray());
        Assert.Empty((await GetAsync(client, Path + $"?type=operators&accountId={actorId:D}")).EnumerateArray());
        Assert.Empty((await GetAsync(client, Path + "?type=signins")).EnumerateArray());
    }

    [Fact]
    public async Task Periods_use_the_operator_day_and_the_same_rolling_boundaries_as_the_list()
    {
        var initialUtc = factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().UtcDateTime;
        initialUtc = initialUtc.AddTicks(-(initialUtc.Ticks % TimeSpan.TicksPerMicrosecond));
        var clock = new FakeTimeProvider(new DateTimeOffset(initialUtc));
        await using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, [PlatformPermissions.Audit.Read], Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        var nowUtc = clock.GetUtcNow().UtcDateTime;
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, zone);
        var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local.Date, DateTimeKind.Unspecified), zone);
        var tag = "periodos-" + Guid.NewGuid().ToString("N");
        var dates = new[] { startUtc.AddMinutes(1), startUtc.AddMinutes(-1), nowUtc.AddDays(-7),
            nowUtc.AddDays(-7).AddMinutes(-1), nowUtc.AddDays(-30), nowUtc.AddDays(-30).AddMinutes(-1) };
        var tenants = dates.Select((_, index) => Business(tag + " " + index)).ToArray();
        await SaveAsync(tenants, tenants.Select((tenant, index) => SecurityEvent.ForTenantAdministration(
            SecurityEventType.OrganizationSuspended, account.UserId, tenant.Id, tag, dates[index])));

        foreach (var (period, count) in new[] { ("0", 1), ("7", 3), ("30", 5), ("all", 6) })
        {
            var filters = $"?period={period}&search={tag}&type=organizations";
            var options = await GetAsync(client, Path + filters);
            Assert.Equal(count, options.GetArrayLength());
            var events = await GetAsync(client, "/api/platform/audit" + filters);
            Assert.Equal(events.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("targetTenantId").GetGuid()).Order(),
                options.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Order());
        }
    }

    [Fact]
    public async Task Only_platform_auditors_can_read_options_without_tenants_read()
    {
        using var anonymous = Client();
        using var unauthenticated = await anonymous.GetAsync(Path, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        using var person = Client();
        var account = await AccountJourney.RegisterAsync(factory, person, Ct);
        using var consumer = await person.GetAsync(Path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, consumer.StatusCode);
        var tenantId = await OrganizationJourney.StageAsync(factory, account, "Business " + Guid.NewGuid().ToString("N"), Ct);
        await AccountJourney.AuthorizeAsync(person, "business", Ct, tenantId);
        using var business = await person.GetAsync(Path, Ct);
        Assert.Equal(HttpStatusCode.Forbidden, business.StatusCode);
        var (reader, _) = await OperatorAsync([PlatformPermissions.Tenants.Read]);
        using (reader)
        using (var forbidden = await reader.GetAsync(Path, Ct))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var (auditor, _) = await OperatorAsync([PlatformPermissions.Audit.Read]);
        using (auditor)
            await GetAsync(auditor, Path);
    }

    [Theory]
    [InlineData("after=bad!", "after")]
    [InlineData("limit=0", "limit")]
    [InlineData("period=31", "period")]
    [InlineData("type=other", "type")]
    [InlineData("tenantId=00000000-0000-0000-0000-000000000000", "tenantId")]
    [InlineData("accountId=00000000-0000-0000-0000-000000000000", "accountId")]
    [InlineData("search", "search")]
    public async Task Options_validate_the_same_query_as_the_list(string query, string field)
    {
        var (platform, _) = await OperatorAsync([PlatformPermissions.Audit.Read]);
        using var client = platform;
        if (field == "search") query = "search=" + new string('a', TextLimits.AuditSearch + 1);
        using var response = await client.GetAsync(Path + "?" + query, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("Validation.Failed", document.RootElement.GetProperty("code").GetString());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        Assert.True(document.RootElement.TryGetProperty("traceId", out _));
    }

    private static Tenant Business(string name)
    {
        var tenant = Tenant.CreateBusiness(name, false);
        Assert.True(tenant.Activate().IsSuccess);
        return tenant;
    }

    private async Task SaveAsync(IEnumerable<Tenant> tenants, IEnumerable<SecurityEvent> events)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(_ =>
        {
            foreach (var tenant in tenants) services.GetRequiredService<ITenantRepository>().Add(tenant);
            services.GetRequiredService<ApplicationDbContext>().SecurityEvents.AddRange(events);
            return Task.FromResult(Result.Success());
        }, CommitPolicy.OnSuccess, Ct);
    }

    private HttpClient Client() => factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private async Task<(HttpClient Client, Guid Id)> OperatorAsync(IReadOnlyCollection<string> permissions)
    {
        var client = Client();
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        await PlatformOperators.GrantAsync(factory.Services, account.UserId, permissions, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return (client, account.UserId);
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }
}
