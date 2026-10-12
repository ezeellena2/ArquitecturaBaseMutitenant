using System.Net;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.Export;
using PdfSharp.Pdf.IO;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

[Collection(ApiTestGroup.Name)]
public sealed class AssistantExportTests(ApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Pdf_is_unicode_paginated_and_markdown_is_utf8_without_original_bytes()
    {
        var now = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
        var conversation = new AssistantConversationSummaryResponse(Guid.NewGuid(), "Información del árbol y Ñandú", false, 1, now, now);
        var messages = Enumerable.Range(0, 100).Select(index => new AssistantMessageResponse(Guid.NewGuid(), AssistantMessageRole.Assistant,
            "Información del árbol y Ñandú: " + new string('á', 100), AssistantMessageStatus.Complete, now.AddMinutes(index), 1, [], [], null)).ToArray();
        var renderer = new AssistantExportRenderer();
        var markdown = renderer.Render(conversation, messages, AssistantExportFormat.Markdown);
        using (markdown.Content)
        using (var reader = new StreamReader(markdown.Content)) Assert.Contains("Información del árbol y Ñandú", reader.ReadToEnd());
        var pdf = renderer.Render(conversation, messages, AssistantExportFormat.Pdf);
        using (pdf.Content)
        using (var parsed = PdfReader.Open(pdf.Content, PdfDocumentOpenMode.Import)) Assert.True(parsed.PageCount > 1);
    }

    [Fact]
    public async Task Export_reads_only_owned_history_without_provider_or_quota_and_is_a_private_download()
    {
        using var client = AssistantTestData.Client(factory);
        await AccountJourney.RegisterAsync(factory, client, Ct);
        await AssistantTestData.PersonalAsync(factory, true);
        await AssistantExecutionActorTests.AcceptNoticeAsync(client);
        using var quick = await AssistantDurableConversationTests.PostAsync(client, "/api/me/assistant/quick/my_signin", "{}", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, quick.StatusCode);
        using var body = JsonDocument.Parse(await quick.Content.ReadAsStringAsync(Ct));
        var id = body.RootElement.GetProperty("conversation").GetProperty("id").GetGuid();
        foreach (var format in new[] { "markdown", "pdf" })
        {
            using var result = await client.GetAsync($"/api/me/assistant/conversations/{id}/export?format={format}", Ct);
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.True(result.Headers.CacheControl!.NoStore);
            Assert.Equal("attachment", result.Content.Headers.ContentDisposition!.DispositionType);
            Assert.NotEmpty(await result.Content.ReadAsByteArrayAsync(Ct));
        }
        using var other = AssistantTestData.Client(factory);
        await AccountJourney.RegisterAsync(factory, other, Ct);
        await AssistantExecutionActorTests.AcceptNoticeAsync(other);
        using var foreign = await other.GetAsync($"/api/me/assistant/conversations/{id}/export?format=pdf", Ct);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }
}
