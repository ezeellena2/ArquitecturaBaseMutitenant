using System.Text.Json.Nodes;
using ArquitecturaBaseMultitenant.Domain.Assistant;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// Contrato de la segunda parte de la base de la 12 (K1) en el asistente (ADR 0064): las subidas que miran el cupo y el lugar
/// antes del cuerpo, la conversación que no se puede leer y las fallas del proveedor en las dos configuraciones.
/// </summary>
public sealed class AssistantRulesContractTests
{
    private static readonly string[] Bases = ["/api/organization/assistant", "/api/me/assistant", "/api/platform/assistant"];

    [Fact]
    public void Uploads_publish_quota_slot_and_timeout_before_reading_the_body()
    {
        var paths = Contract()["paths"]!;
        foreach (var root in Bases)
        {
            var upload = paths[root + "/conversations/{id}/attachments"]!["post"]!;
            var codes = Codes(upload);
            foreach (var expected in new[]
            {
                "429 " + AssistantErrors.LimitDailyReachedCode, "429 " + AssistantErrors.LimitMonthlyReachedCode,
                "429 " + AssistantErrors.AttachmentUploadBusyCode, "429 " + AssistantErrors.AttachmentUploadQueueFullCode,
                "400 " + AssistantErrors.AttachmentUploadTimedOutCode, "422 Request.IdempotencyKeyReused",
            })
                Assert.Contains(expected, codes);
            var examples = upload["responses"]!["429"]!["content"]!["application/problem+json"]!["examples"]!;
            Assert.Equal(60, examples[AssistantErrors.AttachmentUploadBusyCode + ".es"]!["value"]!["retryAfter"]!.GetValue<int>());
            // Enviar un mensaje no ocupa un lugar de subida.
            Assert.DoesNotContain("429 " + AssistantErrors.AttachmentUploadBusyCode, Codes(paths[root + "/conversations/{id}/messages"]!["post"]!));
        }
    }

    [Fact]
    public void An_unreadable_conversation_is_unavailable_and_can_be_deleted()
    {
        var document = Contract();
        foreach (var root in Bases)
        {
            var conversation = document["paths"]![root + "/conversations/{id}"]!;
            Assert.Contains("409 " + AssistantErrors.ConversationUnavailableCode, Codes(conversation["get"]!));
            Assert.DoesNotContain("409 " + AssistantErrors.ConversationUnavailableCode, Codes(conversation["delete"]!));
            // Tampoco se exporta ni recibe un mensaje nuevo (B14, revisión adversarial).
            Assert.Contains("409 " + AssistantErrors.ConversationUnavailableCode, Codes(document["paths"]![root + "/conversations/{id}/export"]!["get"]!));
            Assert.Contains("409 " + AssistantErrors.ConversationUnavailableCode, Codes(document["paths"]![root + "/conversations/{id}/messages"]!["post"]!));
        }
        var summary = document["components"]!["schemas"]!["AssistantConversationSummaryResponse"]!;
        Assert.Contains("available", summary["required"]!.AsArray().Select(value => value!.GetValue<string>()));
        Assert.Equal("boolean", summary["properties"]!["available"]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Both_configurations_publish_the_provider_issue_and_the_unknown_provider()
    {
        var schemas = Contract()["components"]!["schemas"]!;
        foreach (var name in new[] { "OrganizationAssistantSettingsResponse", "PlatformAssistantSettingsResponse" })
        {
            var settings = schemas[name]!;
            Assert.Contains("unsupportedProvider", settings["required"]!.AsArray().Select(value => value!.GetValue<string>()));
            Assert.DoesNotContain("providerIssue", settings["required"]!.AsArray().Select(value => value!.GetValue<string>()));
            var issue = settings["properties"]!["providerIssue"]!["description"]!.GetValue<string>();
            Assert.All(AssistantErrors.ProviderIssueCodes, code => Assert.Contains(code, issue, StringComparison.Ordinal));
        }
        var eventCode = schemas["AssistantErrorEventResponse"]!["properties"]!["code"]!["description"]!.GetValue<string>();
        Assert.All(AssistantErrors.ProviderFailureCodes, code => Assert.Contains(code, eventCode, StringComparison.Ordinal));
    }

    private static string[] Codes(JsonNode operation) =>
        [.. operation["x-error-codes"]!.AsArray().Select(value => value!["status"]!.GetValue<int>() + " " + value["code"]!.GetValue<string>())];

    private static JsonNode Contract()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ArquitecturaBaseMultitenant.slnx"))) directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        return JsonNode.Parse(File.ReadAllText(Path.Combine(root, "docs", "contracts", "openapi.json")))!;
    }
}
