using ArquitecturaBaseMultitenant.Application.Models.Platform;
using ArquitecturaBaseMultitenant.Application.Resources;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.ValueObjects;

namespace ArquitecturaBaseMultitenant.Api.Contracts.Platform;

public sealed record UpdatePlatformSettingsHttpRequest(ConsumerSignupMode? ConsumerSignup, BusinessSignupMode? BusinessSignup,
    int? MaxOwnedOrganizations, string? SalesEmail, string? SupportEmail, uint? Version, string? Reason)
{
    public override string ToString() => nameof(UpdatePlatformSettingsHttpRequest);

    /// <summary>Los dos correos son obligatorios; vacío o mal escrito da «Ingresá un correo válido.» en su campo.</summary>
    public Result<UpdatePlatformSettingsRequest> ToModel()
    {
        var sales = Email.Create(SalesEmail);
        var support = Email.Create(SupportEmail);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (sales.IsFailure) errors["salesEmail"] = [ValidationTexts.EmailInvalid];
        if (support.IsFailure) errors["supportEmail"] = [ValidationTexts.EmailInvalid];
        if (errors.Count > 0) return new ValidationError(errors);
        return new UpdatePlatformSettingsRequest(ConsumerSignup, BusinessSignup, MaxOwnedOrganizations, sales.Value,
            support.Value, Version, Reason);
    }
}
