namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>De dónde salen las opciones de un catálogo; no cambia después de crearlo (E12, §3).</summary>
public enum FormCatalogSource
{
    Manual,
    Members,
    Contacts,
}
