using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Help;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Help;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Tools;

internal sealed class HelpArticleTool(AssistantAccessPolicy policy, IHelpArticleReader help, IProfileService profiles,
    AssistantHelpAvailabilityPolicy availability) : AssistantTool(policy)
{
    public override string Key => "help_article";
    public override Access? Access => null;
    public override IReadOnlyList<string> Permissions => [];
    protected override IReadOnlyList<AssistantToolParameter> Parameters => [new("topicKey", "string"), new("search", "string")];
    protected override async Task<Result<AssistantToolData>> ExecuteCoreAsync(AssistantToolArguments args, CancellationToken ct)
    {
        var profile = await profiles.GetAsync(ct);
        if (profile.IsFailure) return profile.Error;
        var me = profile.Value;
        var granted = me.Access == Domain.Users.Access.Platform ? me.EffectivePermissions.Platform : me.EffectivePermissions.Organization;
        var modules = new List<string>();
        foreach (var key in help.Catalog.Select(article => article.Module).OfType<string>().Distinct(StringComparer.Ordinal))
            if (await availability.IsAvailableAsync(key, ct)) modules.Add(key);
        var routes = help.Catalog.Where(article => article.Accesses.Contains(me.Access)
            && (article.Permission is null || granted.Contains(article.Permission, StringComparer.Ordinal))
            && (article.Module is null || modules.Contains(article.Module, StringComparer.Ordinal)))
            .Select(article => article.RouteKey).Distinct(StringComparer.Ordinal).ToArray();
        var language = System.Globalization.CultureInfo.GetCultureInfo(me.Culture).TwoLetterISOLanguageName;
        var articles = await help.SearchAsync(new HelpArticleRequest(args.Text("topicKey"), args.Text("search")),
            new HelpArticleContext(me.Access, language, routes, granted, modules), ct);
        var authorized = new List<AssistantHelpArticleResponse>();
        foreach (var article in articles)
        {
            var metadata = help.Catalog.SingleOrDefault(row => row.TopicKey == article.TopicKey && row.RouteKey == article.RouteKey);
            if (metadata is null || !metadata.Accesses.Contains(me.Access)
                || !await AccessPolicy.CanUseAsync(me.Access, metadata.Permission is { } permission ? [permission] : [], null, ct)
                || metadata.Module is { } module && !await availability.IsAvailableAsync(module, ct)) continue;
            authorized.Add(new AssistantHelpArticleResponse(article.TopicKey, article.Title, article.Text));
        }
        return new AssistantHelpToolData(authorized);
    }
}
