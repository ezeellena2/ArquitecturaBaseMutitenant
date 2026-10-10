namespace ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;

/// <summary>
/// Nombres de los índices únicos que la base hace cumplir y que un caso de uso traduce. Infrastructure los usa en
/// <c>HasDatabaseName</c> y el servicio los compara con <c>UniqueConstraintViolationException.ConstraintName</c>
/// para dar su error propio. El control previo del servicio no alcanza ante dos pedidos simultáneos: el índice es la
/// barrera y su choque nunca llega como 500.
/// </summary>
public static class UniqueKeys
{
    public const string AssistantModel = "IX_AssistantModelPrices_Provider_ProviderModelId";
    /// <summary>Un destino verificado pertenece a una sola cuenta por tipo de método.</summary>
    public const string VerifiedLoginMethod = "IX_LoginMethods_Type_Value";

    /// <summary>
    /// El nombre de un rol es único dentro de su empresa; los roles sin empresa (los de toda la organización y los
    /// que valen en cada empresa) comparten un solo espacio de nombres (<c>NULLS NOT DISTINCT</c>).
    /// </summary>
    public const string RoleName = "IX_Roles_TenantId_CompanyId_NormalizedName";

    /// <summary>La dirección pública (slug) de una organización es única en todo el sistema.</summary>
    public const string TenantSlug = "IX_Tenants_Slug";

    /// <summary>Un dominio de correo pertenece a una sola organización mientras no esté quitado.</summary>
    public const string TenantDomain = "IX_TenantDomains_Domain";

    /// <summary>Un módulo tiene una sola fila de configuración por organización.</summary>
    public const string TenantFeature = "IX_TenantFeatures_TenantId_FeatureKey";

    /// <summary>La identificación fiscal de una empresa es única dentro de su organización.</summary>
    public const string CompanyTaxId = "IX_Companies_TenantId_TaxCountry_TaxType_TaxNumber";

    /// <summary>Una organización tiene una sola página pública.</summary>
    public const string PublicPage = "IX_PublicPages_BusinessTenantId";
}
