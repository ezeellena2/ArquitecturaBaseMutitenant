namespace ArquitecturaBaseMultitenant.Domain.Forms;

/// <summary>Lo que no entra en WhatsApp de una opción, según la presentación del catálogo (E13); se calcula al leer.</summary>
public enum FormCatalogPresentationIssue
{
    TitleEmpty,
    TitleTooLong,
    DescriptionTooLong,
}
