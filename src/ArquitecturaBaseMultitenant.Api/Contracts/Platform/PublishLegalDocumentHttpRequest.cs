using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Domain.Legal;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

/// <summary>Una versión nueva: tipo, fecha civil de vigencia (aaaa-mm-dd) y el texto en cada cultura habilitada.</summary>
public sealed record PublishLegalDocumentHttpRequest(LegalDocumentKind? Kind, DateOnly? EffectiveOn,
    IReadOnlyList<LegalDocumentContentHttpRequest>? Contents)
{
    public PublishLegalDocumentRequest ToModel() => new(Kind, EffectiveOn,
        Contents?.Select(content => new LegalDocumentContentInput(content.Culture, content.Text)).ToArray());

    public override string ToString() => nameof(PublishLegalDocumentHttpRequest);
}

public sealed record LegalDocumentContentHttpRequest(string? Culture, string? Text)
{
    public override string ToString() => nameof(LegalDocumentContentHttpRequest);
}
