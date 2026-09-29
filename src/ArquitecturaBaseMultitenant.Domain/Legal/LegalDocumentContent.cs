namespace ArquitecturaBaseMultitenant.Domain.Legal;

public sealed class LegalDocumentContent
{
    private LegalDocumentContent() { }

    public Guid LegalDocumentId { get; private set; }
    public string Culture { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;

    public static LegalDocumentContent Create(Guid legalDocumentId, string culture, string text)
    {
        if (legalDocumentId == Guid.Empty)
        {
            throw new ArgumentException("The legal document id cannot be empty.", nameof(legalDocumentId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new LegalDocumentContent { LegalDocumentId = legalDocumentId, Culture = culture, Text = text };
    }
}
