using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.WhatsApp;

/// <summary>
/// B5 de la E11, contactos: la importación se revisa sin guardar, rechaza filas con su número en el archivo, sin «Sí» deja
/// «Sin respuesta» y no deshace una baja; los filtros no cambian los conteos; editar exige la versión; el ejemplo se vuelve a
/// importar tal cual; otra organización no ve los contactos.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class WhatsAppContactsTests(ApiFactory factory)
{
    private const string Contacts = "/api/organization/whatsapp/contacts";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Import_is_previewed_without_saving_rejects_bad_rows_and_never_undoes_an_unsubscribe()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, phoneNumberId, Ct)).Dispose();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var unsubscribed = WhatsAppAttentionJourney.NewPhone();
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, unsubscribed, "Hola", Ct);
        var existing = Single(await JsonAsync(await client.GetAsync($"{Contacts}?search={Uri.EscapeDataString(unsubscribed)}", Ct)));
        using (var gone = await client.PutAsJsonAsync($"{Contacts}/{Id(existing)}",
            new { name = (string?)null, tags = Array.Empty<string>(), consent = "Unsubscribed", version = existing.GetProperty("version").GetUInt32() }, Ct))
            Assert.Equal(HttpStatusCode.OK, gone.StatusCode);

        var ana = WhatsAppAttentionJourney.NewPhone();
        var beto = WhatsAppAttentionJourney.NewPhone();
        var csv = string.Join("\n", "Nombre;Teléfono;Etiqueta;Acepta promociones", $"Ana;{ana};VIP;Sí",
            $"Beto;{beto};;no", "Mal;123;;", $"Ana otra vez;{ana};;", $"Ya se fue;{unsubscribed};;si");

        var preview = await JsonAsync(await UploadAsync(client, "import/preview", "contactos.csv", csv, "Feria"));
        Assert.Equal((5, 2, 1), (preview.GetProperty("rows").GetInt32(), preview.GetProperty("new").GetInt32(), preview.GetProperty("updated").GetInt32()));
        Assert.Equal([4, 5], preview.GetProperty("rejected").GetProperty("items").EnumerateArray().Select(row => row.GetProperty("row").GetInt32()));
        Assert.Equal(1, (await JsonAsync(await client.GetAsync(Contacts, Ct))).GetProperty("total").GetInt32());

        var imported = await JsonAsync(await UploadAsync(client, "import", "contactos.csv", csv, "Feria"));
        Assert.Equal((2, 1, 2), (imported.GetProperty("imported").GetInt32(), imported.GetProperty("updated").GetInt32(),
            imported.GetProperty("rejected").GetProperty("totalCount").GetInt32()));
        var anaContact = Single(await JsonAsync(await client.GetAsync($"{Contacts}?search={Uri.EscapeDataString(ana)}", Ct)));
        Assert.Equal(("Ana", "Accepted", "Import"), (anaContact.GetProperty("name").GetString(), anaContact.GetProperty("consent").GetString(),
            anaContact.GetProperty("consentSource").GetString()));
        Assert.Equal(["VIP", "Feria"], anaContact.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()));
        var betoContact = Single(await JsonAsync(await client.GetAsync($"{Contacts}?search={Uri.EscapeDataString(beto)}", Ct)));
        Assert.Equal(("NoAnswer", "Import"), (betoContact.GetProperty("consent").GetString(), betoContact.GetProperty("consentSource").GetString()));
        var stillGone = Single(await JsonAsync(await client.GetAsync($"{Contacts}?search={Uri.EscapeDataString(unsubscribed)}", Ct)));
        Assert.Equal("Unsubscribed", stillGone.GetProperty("consent").GetString());

        using var tooMany = await UploadAsync(client, "import/preview", "contactos.csv",
            "Teléfono\n" + string.Join("\n", Enumerable.Range(0, WhatsAppFeature.MaxImportRows + 1).Select(index => "+54911" + (30_000_000 + index))), null);
        Assert.Equal((HttpStatusCode.BadRequest, WhatsAppErrors.ImportTooManyRowsCode), (tooMany.StatusCode, await CodeAsync(tooMany)));
        using var notASheet = await UploadAsync(client, "import/preview", "contactos.txt", "Teléfono\n+5491133334444", null);
        Assert.Equal((HttpStatusCode.BadRequest, WhatsAppErrors.ImportInvalidFileCode), (notASheet.StatusCode, await CodeAsync(notASheet)));
    }

    [Fact]
    public async Task Filters_keep_the_counts_edits_need_the_version_the_sample_imports_back_and_another_organization_sees_nothing()
    {
        await using var host = WhatsAppAttentionJourney.Host(factory, new FakeGraph());
        using var client = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, client, Ct);
        var phoneNumberId = WhatsAppAttentionJourney.NewNumberId();
        (await WhatsAppAttentionJourney.ConnectAsync(client, phoneNumberId, Ct)).Dispose();
        using var anonymous = WhatsAppAttentionJourney.Client(host);
        var writer = WhatsAppAttentionJourney.NewPhone();
        await WhatsAppAttentionJourney.ReceiveAsync(host, anonymous, phoneNumberId, writer, "Quiero precios", Ct);
        var other = WhatsAppAttentionJourney.NewPhone();
        using (var imported = await UploadAsync(client, "import", "c.csv", $"Phone,Tag,Accepts promotions\n{other},VIP,Yes", null))
            Assert.Equal(HttpStatusCode.OK, imported.StatusCode);

        var vip = await JsonAsync(await client.GetAsync($"{Contacts}?tag=VIP", Ct));
        Assert.Equal((1, 2, 1), (vip.GetProperty("contacts").GetProperty("totalCount").GetInt32(), vip.GetProperty("total").GetInt32(),
            vip.GetProperty("accepting").GetInt32()));
        Assert.Contains("VIP", (await JsonAsync(await client.GetAsync($"{Contacts}/tags", Ct))).GetProperty("items").EnumerateArray()
            .Select(tag => tag.GetString()));

        var contact = Single(await JsonAsync(await client.GetAsync($"{Contacts}?search={Uri.EscapeDataString(writer)}", Ct)));
        Assert.NotEqual(JsonValueKind.Null, contact.GetProperty("latestConversationId").ValueKind);
        var conversations = await JsonAsync(await client.GetAsync($"{Contacts}/{Id(contact)}/conversations", Ct));
        Assert.NotNull(Assert.Single(conversations.GetProperty("items").EnumerateArray()).GetProperty("lastMessagePreview").GetString());
        var version = contact.GetProperty("version").GetUInt32();
        using (var edited = await client.PutAsJsonAsync($"{Contacts}/{Id(contact)}",
            new { name = "Carla", tags = new[] { "Mayorista" }, consent = "Accepted", version }, Ct))
        {
            var body = await WhatsAppAttentionJourney.JsonAsync(edited, Ct);
            Assert.Equal(("Carla", "Manual"), (body.GetProperty("name").GetString(), body.GetProperty("consentSource").GetString()));
        }
        using (var stale = await client.PutAsJsonAsync($"{Contacts}/{Id(contact)}", new { name = "Otra", tags = Array.Empty<string>(), version }, Ct))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var sample = await client.GetAsync($"{Contacts}/import-sample", Ct);
        Assert.Equal(HttpStatusCode.OK, sample.StatusCode);
        var bytes = await sample.Content.ReadAsByteArrayAsync(Ct);
        using var content = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "ejemplo.xlsx" } };
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Contacts}/import/preview") { Content = content };
        using var back = await client.SendAsync(request, Ct);
        var preview = await WhatsAppAttentionJourney.JsonAsync(back, Ct);
        Assert.Equal((1, 0), (preview.GetProperty("rows").GetInt32(), preview.GetProperty("rejected").GetProperty("totalCount").GetInt32()));

        using var outsider = WhatsAppAttentionJourney.Client(host);
        await WhatsAppAttentionJourney.OrganizationAsync(factory, host, outsider, Ct);
        using var foreign = await outsider.GetAsync($"{Contacts}/{Id(contact)}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string path, string fileName, string text, string? tag)
    {
        var content = new MultipartFormDataContent { { new ByteArrayContent(Encoding.UTF8.GetBytes(text)), "file", fileName } };
        if (tag is not null) content.Add(new StringContent(tag), "tag");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Contacts}/{path}") { Content = content };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return client.SendAsync(request, Ct);
    }

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        using (response) return await WhatsAppAttentionJourney.JsonAsync(response, Ct);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await WhatsAppAttentionJourney.JsonAsync(response, Ct)).GetProperty("code").GetString();

    private static JsonElement Single(JsonElement list) => Assert.Single(list.GetProperty("contacts").GetProperty("items").EnumerateArray());

    private static Guid Id(JsonElement element) => element.GetProperty("id").GetGuid();
}
