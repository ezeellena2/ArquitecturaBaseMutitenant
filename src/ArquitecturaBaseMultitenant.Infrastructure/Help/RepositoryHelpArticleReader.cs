using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Help;
using ArquitecturaBaseMultitenant.Application.Models.Help;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Infrastructure.Help;

/// <summary>Only trusted embedded manifest paths can be read; the chat supplies topic/search, never a path.</summary>
internal sealed class RepositoryHelpArticleReader : IHelpArticleReader
{
    private static readonly Assembly Assembly = typeof(RepositoryHelpArticleReader).Assembly;
    private static readonly JsonSerializerOptions ManifestOptions = CreateOptions();
    private readonly IReadOnlyList<ManifestArticle> _articles = LoadManifest();
    public IReadOnlyList<HelpArticleMetadata> Catalog => _articles.Select(article =>
        new HelpArticleMetadata(article.TopicKey, article.RouteKey, article.Accesses, article.Permission, article.Module)).ToArray();

    public async Task<IReadOnlyList<HelpArticleDocument>> SearchAsync(HelpArticleRequest request, HelpArticleContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (context.Language is not ("es" or "en") || request.Search?.Length > 100 || request.TopicKey?.Length > 100) return [];
        var matches = _articles.Where(article => article.Accesses.Contains(context.Access)
            && context.AllowedRoutes.Contains(article.RouteKey, StringComparer.Ordinal)
            && (article.Permission is null || context.Permissions.Contains(article.Permission, StringComparer.Ordinal))
            && (article.Module is null || context.Modules.Contains(article.Module, StringComparer.Ordinal))
            && (request.TopicKey is null || article.TopicKey == request.TopicKey)
            && (string.IsNullOrWhiteSpace(request.Search)
                || (article.TopicKey + " " + article.Titles[context.Language]).Contains(request.Search.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(article => article.TopicKey, StringComparer.Ordinal).Take(3);
        var documents = new List<HelpArticleDocument>();
        foreach (var article in matches)
        {
            var path = article.Files[context.Language];
            using var stream = Assembly.GetManifestResourceStream(ResourceName(path))
                ?? throw new InvalidOperationException("A help article declared by the trusted manifest is missing.");
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync(ct);
            documents.Add(new HelpArticleDocument(article.TopicKey, article.RouteKey, article.Titles[context.Language], text));
        }
        return documents;
    }

    private static string ResourceName(string path)
    {
        if (path.Contains("..", StringComparison.Ordinal) || path.Contains(':') || path.StartsWith('/') || path.Contains('\\')
            || !path.EndsWith(".md", StringComparison.Ordinal)) throw new InvalidOperationException("The trusted help manifest contains an invalid relative path.");
        return "AssistantHelp." + path.Replace('/', '.');
    }

    private static ManifestArticle[] LoadManifest()
    {
        using var stream = Assembly.GetManifestResourceStream("AssistantHelp.index.json")
            ?? throw new InvalidOperationException("The trusted help manifest is missing.");
        var articles = JsonSerializer.Deserialize<ManifestArticle[]>(stream, ManifestOptions)
            ?? throw new InvalidOperationException("The trusted help manifest is invalid.");
        if (articles.Select(article => article.TopicKey).Distinct(StringComparer.Ordinal).Count() != articles.Length)
            throw new InvalidOperationException("Help topic keys must be unique.");
        foreach (var article in articles)
            foreach (var language in new[] { "es", "en" })
            {
                if (!article.Files.TryGetValue(language, out var file) || !article.Titles.ContainsKey(language)
                    || !Assembly.GetManifestResourceNames().Contains(ResourceName(file), StringComparer.Ordinal))
                    throw new InvalidOperationException("Each help topic requires both languages.");
            }
        return articles;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter<Access>());
        return options;
    }

    private sealed record ManifestArticle(string TopicKey, string RouteKey, IReadOnlyList<Access> Accesses,
        string? Permission, string? Module, Dictionary<string, string> Titles, Dictionary<string, string> Files);
}
