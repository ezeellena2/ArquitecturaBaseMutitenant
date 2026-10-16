using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;

namespace ArquitecturaBaseMultitenant.Api.OpenApi;

/// <summary>
/// Reglas transversales del contrato E9, aplicadas después de todos los contribuidores para que ninguno las quite: toda
/// ruta de la API puede responder la cuota compartida y las escrituras sensibles de Plataforma piden la prueba reciente.
/// </summary>
internal sealed partial class SharedContractRules(IApiDescriptionGroupCollectionProvider apis)
{
    public const string RecentProofExtension = "x-recent-authentication";

    public void Apply(OpenApiDocument document)
    {
        var quota = HttpContractDocumentation.CoreError(429, RateLimitErrors.ExceededCode);
        var proof = HttpContractDocumentation.CoreError(403, PlatformReauthenticationErrors.RequiredCode);
        foreach (var api in apis.ApiDescriptionGroups.Items.SelectMany(group => group.Items))
        {
            if (api.HttpMethod is not { } verb || api.RelativePath is not { } relative || Method(verb) is not { } method) continue;
            var path = "/" + RouteConstraint().Replace(relative, "{$1}");
            if (!path.StartsWith("/api/", StringComparison.Ordinal)) continue;
            HttpContractDocumentation.AddErrors(document, path, method, [quota]);
            if (!RecentPlatformProof.IsRequired(api.ActionDescriptor, verb)) continue;
            HttpContractDocumentation.AddErrors(document, path, method, [proof]);
            if (HttpContractDocumentation.Find(document, path, method) is { } operation)
            {
                operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                operation.Extensions[RecentProofExtension] = new JsonNodeExtension(JsonValue.Create(true));
            }
        }
    }

    private static HttpMethod? Method(string verb) => verb.ToUpperInvariant() switch
    {
        "GET" => HttpMethod.Get, "POST" => HttpMethod.Post, "PUT" => HttpMethod.Put, "DELETE" => HttpMethod.Delete,
        "PATCH" => HttpMethod.Patch, "HEAD" => HttpMethod.Head, _ => null,
    };

    [GeneratedRegex(@"\{\*?([^}:?=]+)[^}]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex RouteConstraint();
}
