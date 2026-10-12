using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.AssistantBrowser;

public static class RecorridoAssistantModule
{
    public static void Configure(IWebHostBuilder builder) => builder
        .UseSetting("Assistant:UseFakeChatClient", "true")
        .UseSetting("Assistant:IsolatedE2E", "true")
        .UseSetting("Assistant:TurnWorkerEnabled", "true")
        .UseSetting("Assistant:Platform:ApiKey", "isolated-assistant-provider-key")
        .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Assistant:UseFakeChatClient"] = "true",
            ["Assistant:IsolatedE2E"] = "true",
            ["Assistant:TurnWorkerEnabled"] = "true",
            ["Assistant:Platform:ApiKey"] = "isolated-assistant-provider-key",
        }));
}
