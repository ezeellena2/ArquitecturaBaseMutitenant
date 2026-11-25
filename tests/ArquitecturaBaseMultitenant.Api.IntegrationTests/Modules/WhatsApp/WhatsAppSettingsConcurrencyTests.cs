using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B18 (270 y 272): dos operadores guardan a la vez la configuración de WhatsApp de la plataforma, todavía sin fila, con la
/// versión 0 que leyeron. El lock de la configuración hace esperar al segundo hasta que el primero confirma: el segundo lee la
/// fila nueva y recibe el 409 de siempre, y queda lo del primero. Sin el lock, los dos insertarían la misma fila.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppSettingsConcurrencyTests
{
    private const string Settings = "/api/platform/whatsapp/settings";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Two_operators_saving_the_platform_settings_at_once_keep_the_first_and_the_second_gets_a_conflict()
    {
        await using var isolated = new ApiFactory();
        await isolated.InitializeAsync();
        var hold = new CommitHold(context => context.ChangeTracker.Entries<WhatsAppPlatformSettings>().Any());
        await using var host = isolated.WithWebHostBuilder(hold.Install);
        using var first = await OperatorAsync(host);
        using var second = await OperatorAsync(host);
        using (var before = await first.GetAsync(Settings, Ct))
            Assert.Equal(0u, (await JsonAsync(before)).GetProperty("version").GetUInt32());

        var (saved, refused) = await hold.RaceAsync(host.Services,
            () => first.PutAsJsonAsync(Settings, new { allowedCountries = new[] { "AR" }, chatRetentionDays = 60, maxOrganizationChatRetentionDays = 365, version = 0 }, Ct),
            () => second.PutAsJsonAsync(Settings, new { allowedCountries = new[] { "AR" }, chatRetentionDays = 30, maxOrganizationChatRetentionDays = 180, version = 0 }, Ct),
            Ct);

        using (var ok = await saved)
            Assert.True(ok.StatusCode == HttpStatusCode.OK, $"{(int)ok.StatusCode}: {await ok.Content.ReadAsStringAsync(Ct)}");
        using (var conflict = await refused)
        {
            Assert.True(conflict.StatusCode == HttpStatusCode.Conflict, $"{(int)conflict.StatusCode}: {await conflict.Content.ReadAsStringAsync(Ct)}");
            Assert.Equal("General.ConcurrencyConflict", (await JsonAsync(conflict)).GetProperty("code").GetString());
        }
        using var after = await second.GetAsync(Settings, Ct);
        var current = await JsonAsync(after);
        Assert.Equal((60, 365), (current.GetProperty("chatRetentionDays").GetInt32(), current.GetProperty("maxOrganizationChatRetentionDays").GetInt32()));
        Assert.NotEqual(0u, current.GetProperty("version").GetUInt32());
    }

    private static async Task<HttpClient> OperatorAsync(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var account = await AccountJourney.RegisterAsync(host, client, Ct);
        await PlatformOperators.GrantAsync(host.Services, account.UserId, PlatformOperators.All, Ct);
        await AccountJourney.AuthorizeAsync(client, "platform", Ct);
        return client;
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return document.RootElement.Clone();
    }
}
