using System.Net;
using System.Text;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Domain.Assistant;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;
using Microsoft.Extensions.AI;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantProviderAdapterTests
{
    [Fact]
    public async Task Chat_call_does_not_expose_provider_exception_or_body()
    {
        var handler = new RecordingHandler(503);
        using var client = AnthropicChatClientFactory.Build("isolated-adapter-secret", "claude-sonnet-5-5", AssistantEffort.Low, 2048, new HttpClient(handler));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "Test")], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("isolated-sensitive-provider-payload", error.ToString());
    }
    [Fact]
    public void Own_key_cipher_is_bound_to_the_organization_and_business_identity()
    {
        var provider = new EphemeralDataProtectionProvider();
        var first = new ExecutionContextStub();
        var protector = new AssistantKeyProtector(provider, first, first);
        var cipher = protector.ProtectOrganization("isolated-purpose-secret");
        Assert.Equal("isolated-purpose-secret", protector.UnprotectOrganization(cipher));
        var second = new ExecutionContextStub();
        Assert.Throws<CryptographicException>(() => new AssistantKeyProtector(provider, second, second).UnprotectOrganization(cipher));
        first.SelectedAccess = Access.Consumer;
        Assert.Throws<InvalidOperationException>(() => protector.UnprotectOrganization(cipher));
        first.SelectedAccess = Access.Platform;
        Assert.Throws<CryptographicException>(() => protector.UnprotectPlatform(cipher));
    }
    [Theory]
    [InlineData(200, null)]
    [InlineData(401, AssistantErrors.SettingsKeyRejectedCode)]
    [InlineData(403, AssistantErrors.SettingsKeyRejectedCode)]
    [InlineData(429, AssistantErrors.ProviderUnavailableCode)]
    [InlineData(503, AssistantErrors.ProviderUnavailableCode)]
    public async Task Draft_validation_is_minimal_sanitized_without_retries_and_disposes_owned_transport(int status, string? errorCode)
    {
        var handler = new RecordingHandler(status);
        var result = await AnthropicChatClientFactory.ValidateDraftAsync("isolated-adapter-secret", new HttpClient(handler), TestContext.Current.CancellationToken);
        Assert.Equal(1, handler.Requests);
        Assert.True(handler.Disposed);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal("claude-haiku-5-5", request.RootElement.GetProperty("model").GetString());
        Assert.Equal(64, request.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("adaptive", request.RootElement.GetProperty("thinking").GetProperty("type").GetString());
        Assert.Equal("low", request.RootElement.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.DoesNotContain("isolated-adapter-secret", handler.Body);
        if (errorCode is null) Assert.True(result.IsSuccess);
        else
        {
            Assert.Equal(errorCode, result.Error.Code);
            Assert.DoesNotContain("isolated-sensitive-provider-payload", result.Error.Description);
            Assert.DoesNotContain("isolated-adapter-secret", result.Error.Description);
        }
    }
    private sealed class RecordingHandler(int status) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public int Requests { get; private set; }
        public bool Disposed { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var payload = status == 200 ? """
                {"id":"isolated-message","type":"message","role":"assistant","model":"claude-haiku-5-5","content":[{"type":"text","text":"OK"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":1,"output_tokens":1}}
                """ : """
                {"type":"error","error":{"type":"authentication_error","message":"isolated-sensitive-provider-payload"}}
                """;
            return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        }
        protected override void Dispose(bool disposing) { Disposed |= disposing; base.Dispose(disposing); }
    }
    private sealed class ExecutionContextStub : ITenantContext, ICurrentUser
    {
        public Guid? UserId { get; } = Guid.CreateVersion7();
        public Access SelectedAccess { get; set; } = Domain.Users.Access.Business;
        public Access? Access => SelectedAccess;
        public Guid? TenantId { get; } = Guid.CreateVersion7();
        public TenantKind? TenantKind => Domain.Tenancy.TenantKind.Business;
        public Guid RequiredTenantId => TenantId!.Value;
    }
}
