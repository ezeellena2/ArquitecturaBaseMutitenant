using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Results;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantHelpAvailabilityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_defined_feature_gate_cannot_be_overridden_by_an_available_channel()
    {
        var features = new MutableFeature();
        var channel = new ReadOnlyChannel("fixture-module");
        var policy = new AssistantHelpAvailabilityPolicy(features,
            new Catalog([new(channel.Key, FeatureScope.Both, true)]), [channel]);
        Assert.True(await policy.IsAvailableAsync(channel.Key, Ct));
        features.Enabled = false;
        Assert.False(await policy.IsAvailableAsync(channel.Key, Ct));
        Assert.True(channel.IsAvailable);
        Assert.Equal(0, channel.SendCalls);
    }

    [Fact]
    public async Task Only_a_registered_available_channel_grants_a_key_without_a_defined_feature()
    {
        var channel = new ReadOnlyChannel("fixture-module");
        var features = new MutableFeature();
        var policy = new AssistantHelpAvailabilityPolicy(features, new Catalog([]), [channel]);
        Assert.True(await policy.IsAvailableAsync(channel.Key, Ct));
        channel.IsAvailable = false;
        Assert.False(await policy.IsAvailableAsync(channel.Key, Ct));
        Assert.False(await policy.IsAvailableAsync("unknown-module", Ct));
        var removed = new AssistantHelpAvailabilityPolicy(features, new Catalog([]), []);
        Assert.False(await removed.IsAvailableAsync(channel.Key, Ct));
        Assert.Equal(0, features.Calls);
        Assert.Equal(0, channel.SendCalls);
    }

    private sealed record Catalog(IReadOnlyList<FeatureDefinition> All) : IFeatureCatalog;
    private sealed class MutableFeature : IFeatureService
    {
        internal bool Enabled { get; set; } = true;
        internal int Calls { get; private set; }
        public Task<bool> IsEnabledAsync(string key, CancellationToken ct) { Calls++; return Task.FromResult(Enabled); }
        public Task<IReadOnlyList<string>> ListEnabledAsync(CancellationToken ct) => throw new InvalidOperationException("Per-key gate expected.");
    }
    private sealed class ReadOnlyChannel(string key) : ILoginCodeChannel
    {
        public string Key => key;
        public bool IsAvailable { get; set; } = true;
        internal int SendCalls { get; private set; }
        private InvalidOperationException UnexpectedCall() { SendCalls++; return new("Only Key and IsAvailable can be read."); }
        public LoginMethodType SupportedMethodType => throw UnexpectedCall();
        public IReadOnlyList<string> Countries => throw UnexpectedCall();
        public string? ContactPhone => throw UnexpectedCall();
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => throw UnexpectedCall();
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw UnexpectedCall();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw UnexpectedCall();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw UnexpectedCall();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw UnexpectedCall();
    }
}
