using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantBrowserRequestTests
{
    [Theory]
    [InlineData("prepare", "{\"email\":\"actor@example.test\",\"access\":\"consumer\"}")]
    [InlineData("quotas", "{\"email\":\"actor@example.test\",\"access\":\"business\",\"remainingMessages\":10,\"monthlyFraction\":0.8}")]
    [InlineData("clock", "{\"email\":\"actor@example.test\",\"access\":\"platform\",\"retentionExpired\":true}")]
    [InlineData("providerCounters", "{}")]
    public void Owned_synthetic_scenarios_keep_their_correlation(string kind, string args)
    {
        var id = Guid.NewGuid();
        var request = AssistantBrowserRequest.Parse($$"""{"id":"{{id}}","kind":"{{kind}}","args":{{args}}}""");
        Assert.Equal(id, request.Id);
        Assert.Equal(kind, request.Kind);
    }

    [Theory]
    [InlineData("prepare", "{\"email\":\"person@example.com\",\"access\":\"consumer\"}")]
    [InlineData("prepare", "{\"email\":\"actor@example.test\",\"access\":\"consumer\",\"ownerUserId\":\"forged\"}")]
    [InlineData("quotas", "{\"email\":\"actor@example.test\",\"access\":\"consumer\",\"remainingMessages\":49,\"monthlyFraction\":0}")]
    [InlineData("quotas", "{\"email\":\"actor@example.test\",\"access\":\"business\",\"remainingMessages\":0,\"monthlyFraction\":0.1}")]
    [InlineData("clock", "{\"email\":\"actor@example.test\",\"access\":\"platform\",\"retentionExpired\":false}")]
    [InlineData("providerCounters", "{\"reset\":true}")]
    [InlineData("unknown", "{}")]
    public void Commands_cannot_target_real_people_forge_identity_or_reset_provider_calls(string kind, string args)
    {
        Assert.Throws<InvalidOperationException>(() => AssistantBrowserRequest.Parse(
            $$"""{"id":"{{Guid.NewGuid()}}","kind":"{{kind}}","args":{{args}}}"""));
    }

    [Fact]
    public void Unknown_envelope_fields_are_rejected() => Assert.Throws<JsonException>(() =>
        AssistantBrowserRequest.Parse($$"""{"id":"{{Guid.NewGuid()}}","kind":"providerCounters","args":{},"identity":"forged"}"""));
}
