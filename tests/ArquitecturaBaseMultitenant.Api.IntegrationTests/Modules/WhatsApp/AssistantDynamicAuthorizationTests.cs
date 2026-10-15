using ArquitecturaBaseMultitenant.Api.IntegrationTests.Support;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

/// <summary>
/// La ayuda del asistente sobre el artículo de ingreso por WhatsApp: sigue al módulo y solo lee la disponibilidad de su canal.
/// Vive con el módulo WhatsApp y sale con él; el resto de la clase es del asistente.
/// </summary>
public sealed partial class AssistantDynamicAuthorizationTests
{
    [Fact]
    public async Task Help_suppresses_an_article_when_its_catalog_feature_is_disabled_during_search()
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await AssistantTestData.ActiveTenantAsync(client);
        await AssistantTestData.PersonalAsync(factory, true);
        var pause = new ReadPause();
        var counter = new NoChat();
        var probe = new ChannelProbe();
        using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["FeatureManagement:whatsapp"] = "true" }));
            builder.ConfigureTestServices(services =>
            {
                // Desde la E11 la llave whatsapp la declara el módulo instalado; esta prueba la apaga por configuración.
                RegisterHelpChannel(services, true, probe);
                services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new Actor(account.UserId, Access.Consumer)));
                services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
                DecorateChannels(services, probe);
                DecorateHelp(services, pause);
            });
        });
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Personal);
        await AssistantExecutionActorTests.AcceptNoticeAsync(provider);
        var tool = Tool(provider, "help_article");
        var before = await tool.ExecuteAsync(Args(new { topicKey = "whatsapp.signin" }), Ct);
        Assert.Single(Assert.IsType<AssistantHelpToolData>(before.Value).Articles);
        pause.Arm();
        var execution = tool.ExecuteAsync(Args(new { topicKey = "whatsapp.signin" }), Ct);
        await pause.WaitAsync(Ct);
        try { provider.GetRequiredService<IConfiguration>()["FeatureManagement:whatsapp"] = "false"; }
        finally { pause.Resume(); }
        var result = await execution;
        Assert.True(await provider.GetRequiredService<IFeatureService>().IsEnabledAsync("assistant", Ct));
        Assert.False(await provider.GetRequiredService<IFeatureService>().IsEnabledAsync("whatsapp", Ct));
        Assert.True(provider.GetRequiredService<IEnumerable<ILoginCodeChannel>>().Single(row => row.Key == "whatsapp").IsAvailable);
        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.IsType<AssistantHelpToolData>(result.Value).Articles);
        Assert.Equal(0, counter.Calls);
        Assert.Equal(0, probe.SendCalls);
    }

    [Theory]
    [InlineData(false, "es-AR")]
    [InlineData(true, "es-AR")]
    [InlineData(false, "en-US")]
    [InlineData(true, "en-US")]
    public async Task Help_follows_the_whatsapp_module_and_never_uses_its_channel(bool enabled, string culture)
    {
        using var client = AssistantTestData.Client(factory);
        var account = await AccountJourney.RegisterAsync(factory, client, Ct);
        var tenant = await AssistantTestData.ActiveTenantAsync(client);
        using (var setup = factory.Services.CreateScope())
        {
            var services = setup.ServiceProvider;
            await services.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async ct =>
            {
                var users = services.GetRequiredService<IUserRepository>();
                var user = (await users.GetByIdAsync(account.UserId, ct))!;
                Assert.True((await users.UpdateProfileAsync(account.UserId, null, null, culture,
                    user.TimeZoneId, user.ProfileVersion, ct)).IsSuccess);
                return Result.Success();
            }, CommitPolicy.OnSuccess, Ct);
        }
        await AssistantTestData.PersonalAsync(factory, true);
        var pause = new ReadPause();
        var counter = new NoChat();
        var probe = new ChannelProbe();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ICurrentUser>(new Actor(account.UserId, Access.Consumer)));
            services.Replace(ServiceDescriptor.Singleton<IChatClientFactory>(counter));
            RegisterHelpChannel(services, enabled, probe);
            DecorateChannels(services, probe);
            DecorateHelp(services, pause);
        }));
        using var scope = host.Services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<ITenantAccessInitializer>().SetFromAccess(tenant, TenantKind.Personal);
        await AssistantExecutionActorTests.AcceptNoticeAsync(provider);
        // Desde la E11 la llave whatsapp es un módulo (para las personas, «Mensajes»): la ayuda sigue al módulo, que manda
        // sobre el canal que comparte su llave; el canal solo se lee y nunca envía.
        Assert.True(await provider.GetRequiredService<IFeatureService>().IsEnabledAsync("whatsapp", Ct));
        var channel = provider.GetRequiredService<IEnumerable<ILoginCodeChannel>>().Single(row => row.Key == "whatsapp");
        Assert.Equal(enabled, channel.IsAvailable);
        var tool = Tool(provider, "help_article");
        var before = await tool.ExecuteAsync(Args(new { topicKey = "whatsapp.signin" }), Ct);
        Assert.True(before.IsSuccess);
        var articles = Assert.IsType<AssistantHelpToolData>(before.Value).Articles;
        Assert.Equal(culture == "en-US" ? "WhatsApp sign-in" : "Ingreso por WhatsApp", Assert.Single(articles).Title);
        pause.Arm();
        var execution = tool.ExecuteAsync(Args(new { topicKey = "whatsapp.signin" }), Ct);
        await pause.WaitAsync(Ct);
        try { probe.Disabled = true; }
        finally { pause.Resume(); }
        var result = await execution;
        Assert.False(channel.IsAvailable);
        Assert.True(await provider.GetRequiredService<IFeatureService>().IsEnabledAsync("assistant", Ct));
        Assert.True(result.IsSuccess);
        Assert.Single(Assert.IsType<AssistantHelpToolData>(result.Value).Articles);
        Assert.Equal(0, counter.Calls);
        Assert.Equal(0, probe.SendCalls);
    }

    private static void DecorateChannels(IServiceCollection services, ChannelProbe probe)
    {
        foreach (var original in services.Where(row => row.ServiceType == typeof(ILoginCodeChannel)).ToArray())
        {
            services.Remove(original);
            services.Add(ServiceDescriptor.Describe(typeof(ILoginCodeChannel), provider =>
                new GuardedChannel((ILoginCodeChannel)Original(provider, original), probe), original.Lifetime));
        }
    }
    private static void RegisterHelpChannel(IServiceCollection services, bool available, ChannelProbe probe)
    {
        // This direct-tool host needs only the neutral capability, regardless of installed modules.
        services.RemoveAll<ILoginCodeChannel>();
        services.AddSingleton<ILoginCodeChannel>(new HelpLoginCodeChannel(available, probe));
    }
    private sealed class HelpLoginCodeChannel(bool available, ChannelProbe probe) : ILoginCodeChannel
    {
        public string Key => "whatsapp";
        public LoginMethodType SupportedMethodType => LoginMethodType.Phone;
        public bool IsAvailable => available;
        public IReadOnlyList<string> Countries => [];
        public string? ContactPhone => null;
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => throw probe.UnexpectedSend();
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw probe.UnexpectedSend();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
    }
    private sealed class ChannelProbe
    {
        internal bool Disabled { get; set; }
        internal int SendCalls { get; private set; }
        internal InvalidOperationException UnexpectedSend() { SendCalls++; return new("Help must only read channel availability."); }
    }
    private sealed class GuardedChannel(ILoginCodeChannel inner, ChannelProbe probe) : ILoginCodeChannel
    {
        public string Key => inner.Key;
        public LoginMethodType SupportedMethodType => inner.SupportedMethodType;
        public bool IsAvailable => !probe.Disabled && inner.IsAvailable;
        public IReadOnlyList<string> Countries => inner.Countries;
        public string? ContactPhone => inner.ContactPhone;
        public Result ValidateNewDestination(LoginCodeDestination destination, string? country) => throw probe.UnexpectedSend();
        public string RenderLoginCode(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
        public void EnqueueRenderedLoginCode(string payload, Guid? userId = null) => throw probe.UnexpectedSend();
        public void EnqueueSignup(string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
        public void EnqueueVerification(Guid? userId, string destination, string code, int lifetimeMinutes, CultureProfile culture) => throw probe.UnexpectedSend();
    }
}
