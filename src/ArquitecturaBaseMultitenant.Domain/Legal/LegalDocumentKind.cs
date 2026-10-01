namespace ArquitecturaBaseMultitenant.Domain.Legal;

/// <summary>
/// Distingue términos y condiciones de política de privacidad. Cada documento conserva sus propias
/// versiones, textos y aceptaciones.
/// </summary>
public enum LegalDocumentKind
{
    Terms,
    Privacy,
}
