using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Models.Legal;

/// <summary>Versión y texto vigente de un documento legal en una cultura.</summary>
public sealed record LegalDocumentRow(
    Guid Id, LegalDocumentKind Kind, int Version, DateTime EffectiveAtUtc,
    string Culture, string Text);
