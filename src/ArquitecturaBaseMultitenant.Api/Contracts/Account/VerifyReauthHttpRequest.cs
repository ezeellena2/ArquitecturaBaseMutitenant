using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Account;

/// <summary>
/// Recibe la acción, los métodos involucrados y el código que demuestra titularidad. RawText conserva el
/// código recibido y ToString evita exponerlo en logs.
/// </summary>
public sealed record VerifyReauthHttpRequest(ReauthAction? Action, Guid? TargetMethodId, Guid? SourceMethodId,
    [property: RawText] string? Code)
{
    public override string ToString() => nameof(VerifyReauthHttpRequest);
}
