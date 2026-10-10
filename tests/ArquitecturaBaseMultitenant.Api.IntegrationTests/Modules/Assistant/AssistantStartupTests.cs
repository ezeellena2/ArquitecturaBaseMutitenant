using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Integrations;
using ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.Modules.Assistant;

public sealed class AssistantStartupTests
{
    [Theory]
    [InlineData("Production", false, false)]
    [InlineData("Development", false, false)]
    [InlineData("E2E", false, false)]
    [InlineData("Testing", false, true)]
    [InlineData("E2E", true, true)]
    public void Fake_is_restricted_to_testing_or_an_explicitly_isolated_e2e_host(string environment, bool isolated, bool allowed)
    {
        using var provider = Provider(environment, true, isolated);
        if (allowed) provider.GetRequiredService<IStartupValidator>().Validate();
        else Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void Missing_provider_key_does_not_fail_startup_or_create_a_client()
    {
        using var provider = Provider("Production", false, false);
        provider.GetRequiredService<IStartupValidator>().Validate();
        Assert.Null(provider.GetRequiredService<IOptions<AssistantOptions>>().Value.Platform.ApiKey);
        Assert.Null(provider.GetService<Microsoft.Extensions.AI.IChatClient>());
        var references = provider.GetRequiredService<IAssistantProviderCatalog>();
        Assert.Equal(4, references.All.Count);
        Assert.All(references.All, row => Assert.Equal("USD", row.InputPerMillion.Currency.Value));
        Assert.Equal(0.25m, references.Find("claude-fable-5-1")!.CacheReadPerMillion.Amount);
        Assert.Equal(100000, references.Find("claude-haiku-5-5")!.LongPromptThreshold);
        Assert.Equal(0.05m, references.Find("claude-haiku-5-5")!.LongCacheReadPerMillion!.Amount);
    }

    [Fact]
    public void Technical_references_can_be_extended_without_recompiling_or_changing_the_http_contract()
    {
        var file = Path.Combine(Path.GetTempPath(), "assistant-reference-" + Guid.CreateVersion7().ToString("N") + ".json");
        try
        {
            File.WriteAllText(file, """
                [{"currency":"USD","revision":"isolated-fixture-v2","providerModelId":"isolated-compatible-model","nameEs":"Modelo de prueba","nameEn":"Test model","inputPerMillion":1,"outputPerMillion":2,"cacheWrite5MinutesPerMillion":1.25,"cacheWrite1HourPerMillion":2,"cacheReadPerMillion":0.1,"text":true,"images":true,"pdf":true,"tools":true,"efforts":["Low"]}]
                """);
            using var provider = Provider("Testing", false, false, file);
            var references = provider.GetRequiredService<IAssistantProviderCatalog>();
            Assert.Equal(5, references.All.Count);
            Assert.Equal(4, references.InitialModels.Count);
            Assert.Equal("isolated-fixture-v2", references.Find("isolated-compatible-model")!.Revision);
            Assert.Equal("USD", references.Find("isolated-compatible-model")!.InputPerMillion.Currency.Value);
        }
        finally { File.Delete(file); }
    }

    [Theory]
    [InlineData("currency", "\"bad\"")]
    [InlineData("currency", "\"EUR\"")]
    [InlineData("revision", "\" \"")]
    [InlineData("providerModelId", "\"\"")]
    [InlineData("nameEs", "\"\"")]
    [InlineData("nameEn", "\"\"")]
    [InlineData("inputPerMillion", "-1")]
    [InlineData("cacheReadPerMillion", "-0.1")]
    [InlineData("efforts", "[]")]
    [InlineData("efforts", "[\"Low\",\"Low\"]")]
    [InlineData("efforts", "[\"999\"]")]
    [InlineData("longPromptThreshold", "100000")]
    [InlineData("longInputPerMillion", "2")]
    public void Invalid_host_metadata_is_rejected(string property, string value)
    {
        var file = Path.Combine(Path.GetTempPath(), "assistant-reference-invalid-" + Guid.CreateVersion7().ToString("N") + ".json");
        try
        {
            var row = JsonNode.Parse("""
                {"currency":"USD","revision":"fixture-v2","providerModelId":"isolated-model","nameEs":"Prueba","nameEn":"Test","inputPerMillion":1,"outputPerMillion":2,"cacheWrite5MinutesPerMillion":1.25,"cacheWrite1HourPerMillion":2,"cacheReadPerMillion":0.1,"text":true,"images":true,"pdf":true,"tools":true,"efforts":["Low"]}
                """)!;
            row[property] = JsonNode.Parse(value);
            File.WriteAllText(file, new JsonArray(row).ToJsonString());
            using var provider = Provider("Testing", false, false, file);
            Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IAssistantProviderCatalog>());
        }
        finally { File.Delete(file); }
    }

    private static ServiceProvider Provider(string environment, bool fake, bool isolated, string? references = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new EnvironmentStub { EnvironmentName = environment });
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Assistant:UseFakeChatClient"] = fake.ToString(), ["Assistant:IsolatedE2E"] = isolated.ToString(),
            ["Assistant:ProviderReferenceFile"] = references,
        }).Build());
        ArquitecturaBaseMultitenant.Application.Modules.Assistant.AssistantModule.AddAssistantModule(services);
        AssistantModule.AddAssistantModule(services);
        return services.BuildServiceProvider();
    }
    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "AssistantStartupTests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
