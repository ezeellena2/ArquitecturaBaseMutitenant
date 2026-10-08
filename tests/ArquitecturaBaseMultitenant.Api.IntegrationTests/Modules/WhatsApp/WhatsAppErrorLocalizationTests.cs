using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppErrorLocalizationTests(ApiFactory factory)
{
    [Theory]
    [InlineData("country", "es-AR", 400, "WhatsApp no está habilitado para ese país.")]
    [InlineData("country", "en-US", 400, "WhatsApp is not enabled for that country.")]
    [InlineData("quota", "es-AR", 429, "No podemos mandar más códigos por WhatsApp por ahora. Probá más tarde.")]
    [InlineData("quota", "en-US", 429, "We cannot send more WhatsApp codes right now. Try again later.")]
    public async Task Runtime_module_errors_use_its_resources_in_detail_and_fields(string kind, string language, int status, string expected)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/whatsapp-errors/" + kind);
        request.Headers.AcceptLanguage.ParseAdd(language);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(expected, body.RootElement.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
        if (kind == "country") Assert.Equal(expected, body.RootElement.GetProperty("errors").GetProperty("phone")[0].GetString());
        else Assert.InRange(body.RootElement.GetProperty("retryAfter").GetInt32(), 1, 86400);
    }
}
