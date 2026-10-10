using ArquitecturaBaseMultitenant.Application.Models.Help;

namespace ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Help;

public interface IHelpArticleReader
{
    IReadOnlyList<HelpArticleMetadata> Catalog { get; }
    Task<IReadOnlyList<HelpArticleDocument>> SearchAsync(HelpArticleRequest request, HelpArticleContext context, CancellationToken ct);
}
