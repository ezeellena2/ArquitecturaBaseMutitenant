using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Notifications;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Identity;

/// <summary>
/// La campana (base de la 12) con una fuente de prueba, por HTTP y con la base real: cada acceso lee la suya, marcar
/// leído vale solo para quien marca y un id que no es de un aviso visible no se guarda.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class NotificationsTests(ApiFactory factory)
{
    private const string Shared = "probe:shared";
    private static readonly DateTime SharedAtUtc = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_access_reads_its_bell_and_marking_read_stays_with_the_account_that_marks()
    {
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<INotificationSource, ProbeSource>()));
        using var consumer = Client(host);
        var person = await AccountJourney.RegisterAsync(host, consumer, Ct);
        using var business = Client(host);
        var owner = await AccountJourney.RegisterAsync(host, business, Ct);
        var tenantId = await OrganizationJourney.StageAsync(factory, owner, "Campana " + Guid.NewGuid().ToString("N")[..8], Ct);
        await AccountJourney.AuthorizeAsync(business, "business", Ct, tenantId);
        using var platform = Client(host);
        var operatorAccount = await AccountJourney.RegisterAsync(host, platform, Ct);
        await PlatformOperators.GrantAsync(host.Services, operatorAccount.UserId, PlatformOperators.Reduced, Ct);
        await AccountJourney.AuthorizeAsync(platform, "platform", Ct);

        foreach (var (client, userId, access) in new[]
        {
            (consumer, person.UserId, "Consumer"), (business, owner.UserId, "Business"), (platform, operatorAccount.UserId, "Platform"),
        })
        {
            var bell = await BellAsync(client);
            Assert.Equal([Own(userId), Shared], Ids(bell));
            Assert.Equal(2, bell.GetProperty("unread").GetInt32());
            Assert.All(bell.GetProperty("items").EnumerateArray(), item =>
            {
                Assert.Equal(access, item.GetProperty("params").GetProperty("access").GetString());
                Assert.False(item.GetProperty("read").GetBoolean());
            });
        }

        Assert.Equal(HttpStatusCode.NoContent, await MarkAsync(consumer, Shared));
        Assert.Equal(HttpStatusCode.NoContent, await MarkAsync(consumer, Shared));
        // Un aviso de otra cuenta, o un id inventado, no es visible: responde igual y no se guarda.
        Assert.Equal(HttpStatusCode.NoContent, await MarkAsync(consumer, Own(owner.UserId)));
        Assert.Equal(HttpStatusCode.NoContent, await MarkAsync(consumer, "probe:invented"));
        Assert.Equal(HttpStatusCode.NoContent, await MarkAsync(consumer, new string('x', NotificationItem.IdMaxLength + 1)));

        var mine = await BellAsync(consumer);
        Assert.Equal([false, true], mine.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("read").GetBoolean()));
        Assert.Equal(1, mine.GetProperty("unread").GetInt32());
        var theirs = await BellAsync(business);
        Assert.All(theirs.GetProperty("items").EnumerateArray(), item => Assert.False(item.GetProperty("read").GetBoolean()));
        Assert.Equal(2, theirs.GetProperty("unread").GetInt32());

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await database.NotificationReads.AsNoTracking()
            .Where(read => read.UserId == person.UserId || read.UserId == owner.UserId).ToListAsync(Ct);
        var row = Assert.Single(saved);
        Assert.Equal((person.UserId, Shared), (row.UserId, row.NotificationId));
    }

    private static HttpClient Client(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host) =>
        host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });

    private static string Own(Guid userId) => "probe:own:" + userId.ToString("N");

    private static string[] Ids(JsonElement bell) =>
        [.. bell.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!)];

    private static async Task<JsonElement> BellAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/notifications", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).Clone();
    }

    private static async Task<HttpStatusCode> MarkAsync(HttpClient client, string id)
    {
        using var response = await client.PostAsync("/api/notifications/" + Uri.EscapeDataString(id) + "/read", null, Ct);
        return response.StatusCode;
    }

    /// <summary>Un aviso común a todas las cuentas y uno propio de cada una, más nuevo, con el acceso de quien mira.</summary>
    private sealed class ProbeSource : INotificationSource
    {
        public Task<IReadOnlyList<NotificationItem>> ListAsync(NotificationContext context, CancellationToken cancellationToken)
        {
            var parameters = new Dictionary<string, string> { ["access"] = context.Access.ToString() };
            return Task.FromResult<IReadOnlyList<NotificationItem>>(
            [
                new(Shared, "probe", parameters, SharedAtUtc, "/", Read: false),
                new(Own(context.UserId), "probe", parameters, SharedAtUtc.AddHours(1), "/", Read: false),
            ]);
        }
    }
}
