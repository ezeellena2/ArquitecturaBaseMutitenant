namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>Por qué un catálogo activo «No se puede usar»; se calcula al leer, sin guardarse (E12, §5).</summary>
public enum FormCatalogUnusableReason
{
    NoActiveOptions,
    SourceOutdated,
    ChannelUnavailable,
    RoleMissing,
    CompanyMissing,
}
