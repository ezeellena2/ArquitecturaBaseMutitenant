namespace ArquitecturaBaseMultitenant.Api.Hosting;

/// <summary>
/// Orígenes que un módulo suma a la CSP del SPA (E9), como el SDK de Meta. Sólo amplía <c>script-src</c>,
/// <c>frame-src</c> y <c>connect-src</c>, y sólo con orígenes https exactos o con comodín de subdominio: un módulo nunca
/// abre scripts en línea ni eval. Se retira con el módulo.
/// </summary>
public interface IContentSecurityPolicySource
{
    /// <summary>Directiva → orígenes que suma.</summary>
    IReadOnlyDictionary<string, IReadOnlyList<string>> Sources { get; }
}
