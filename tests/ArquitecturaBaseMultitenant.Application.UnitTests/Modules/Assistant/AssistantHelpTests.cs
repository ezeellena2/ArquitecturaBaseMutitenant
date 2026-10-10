using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Help;
using ArquitecturaBaseMultitenant.Application.Models.Help;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.UnitTests.Modules.Assistant;

public sealed class AssistantHelpTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static ServiceProvider Provider()
    {
        var services = new ServiceCollection();
        ArquitecturaBaseMultitenant.Infrastructure.Modules.Assistant.AssistantModule.AddAssistantModule(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Trusted_help_is_bilingual_and_filters_access_routes_permissions_and_modules_before_reading()
    {
        using var provider = Provider();
        var reader = provider.GetRequiredService<IHelpArticleReader>();
        Assert.Contains(reader.Catalog, row => row.TopicKey == "business.roles");
        foreach (var metadata in reader.Catalog)
        {
            foreach (var language in new[] { "es", "en" })
            {
                var context = new HelpArticleContext(metadata.Accesses[0], language, [metadata.RouteKey],
                    metadata.Permission is { } permission ? [permission] : [], metadata.Module is { } module ? [module] : []);
                var document = Assert.Single(await reader.SearchAsync(new(metadata.TopicKey, null), context, Ct));
                Assert.NotEmpty(document.Title);
                Assert.NotEmpty(document.Text);
                Assert.Equal(metadata.RouteKey, document.RouteKey);
                Assert.Empty(await reader.SearchAsync(new(metadata.TopicKey, null), context with { AllowedRoutes = [] }, Ct));
                if (metadata.Permission is not null)
                    Assert.Empty(await reader.SearchAsync(new(metadata.TopicKey, null), context with { Permissions = [] }, Ct));
                if (metadata.Module is not null)
                    Assert.Empty(await reader.SearchAsync(new(metadata.TopicKey, null), context with { Modules = [] }, Ct));
                var wrongAccess = metadata.Accesses.Contains(Access.Consumer) ? Access.Platform : Access.Consumer;
                if (!metadata.Accesses.Contains(wrongAccess))
                    Assert.Empty(await reader.SearchAsync(new(metadata.TopicKey, null), context with { Access = wrongAccess }, Ct));
            }
        }
    }

    [Fact]
    public async Task Help_never_reads_chat_paths_or_returns_more_than_three_topics()
    {
        using var provider = Provider();
        var reader = provider.GetRequiredService<IHelpArticleReader>();
        var context = new HelpArticleContext(Access.Platform, "en", reader.Catalog.Select(row => row.RouteKey).ToArray(),
            reader.Catalog.Where(row => row.Permission is not null).Select(row => row.Permission!).ToArray(), ["assistant", "whatsapp"]);
        Assert.Equal(3, (await reader.SearchAsync(new(null, null), context, Ct)).Count);
        Assert.Empty(await reader.SearchAsync(new("../../AGENTS.md", null), context, Ct));
        Assert.Empty(await reader.SearchAsync(new("C:/secrets", null), context, Ct));
        Assert.Empty(await reader.SearchAsync(new(null, new string('a', 101)), context, Ct));
    }
}
