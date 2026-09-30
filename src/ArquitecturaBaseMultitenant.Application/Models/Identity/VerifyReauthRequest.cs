using ArquitecturaBaseMultitenant.Domain.Authentication;

namespace ArquitecturaBaseMultitenant.Application.Models.Identity;

/// <summary>Vincula el código de respaldo con la acción y los métodos involucrados sin exponerlo en la representación textual.</summary>
public sealed record VerifyReauthRequest(ReauthAction Action, Guid? TargetMethodId, Guid SourceMethodId, string? Code)
{
    public override string ToString() => nameof(VerifyReauthRequest);
}
