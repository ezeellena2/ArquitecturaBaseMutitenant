using ArquitecturaBaseMultitenant.Domain.Users;

namespace ArquitecturaBaseMultitenant.Application.Models.Help;

public sealed record HelpArticleMetadata(string TopicKey, string RouteKey, IReadOnlyList<Access> Accesses,
    string? Permission, string? Module);
public sealed record HelpArticleRequest(string? TopicKey, string? Search);
public sealed record HelpArticleContext(Access Access, string Language, IReadOnlyList<string> AllowedRoutes,
    IReadOnlyList<string> Permissions, IReadOnlyList<string> Modules);
public sealed record HelpArticleDocument(string TopicKey, string RouteKey, string Title, string Text);
