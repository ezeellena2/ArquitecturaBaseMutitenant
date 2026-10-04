using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Application.Models.Platform;

/// <summary>
/// Publicar una versión: tipo, fecha civil desde la que rige (en la zona del operador) y el texto en cada cultura
/// habilitada. La versión es nueva e inmutable; el número lo asigna el servidor.
/// </summary>
public sealed record PublishLegalDocumentRequest(LegalDocumentKind? Kind, DateOnly? EffectiveOn,
    IReadOnlyList<LegalDocumentContentInput>? Contents)
{
    public override string ToString() => nameof(PublishLegalDocumentRequest);
}

public sealed record LegalDocumentContentInput(string? Culture, string? Text)
{
    public override string ToString() => nameof(LegalDocumentContentInput);
}

public sealed record PublishedLegalDocumentResponse(Guid Id, int Version);

/// <summary>Un tipo de documento con su versión vigente y todas sus versiones, de la más nueva a la más vieja.</summary>
public sealed record PlatformLegalDocumentGroupResponse(LegalDocumentKind Kind, int? CurrentVersion,
    DateTime? CurrentEffectiveAtUtc, IReadOnlyList<PlatformLegalVersionResponse> Versions);

public sealed record PlatformLegalVersionResponse(Guid Id, int Version, DateTime EffectiveAtUtc, bool IsCurrent);

/// <summary>Una versión para leer, con el texto de cada cultura. No se edita: para cambiarla se publica otra.</summary>
public sealed record PlatformLegalDocumentResponse(Guid Id, LegalDocumentKind Kind, int Version,
    DateTime EffectiveAtUtc, bool IsCurrent, IReadOnlyList<PlatformLegalContentResponse> Contents);

public sealed record PlatformLegalContentResponse(string Culture, string Text);

/// <summary>Proyección del reader: una versión sin su texto.</summary>
public sealed record LegalVersionRow(Guid Id, LegalDocumentKind Kind, int Version, DateTime EffectiveAtUtc);
