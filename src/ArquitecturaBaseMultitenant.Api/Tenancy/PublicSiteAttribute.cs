using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Tenancy;

/// <summary>Declara que una ruta requiere el subdominio de una organización publicada y activa.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class PublicSiteAttribute() : TypeFilterAttribute(typeof(PublicSiteFilter))
{
}
