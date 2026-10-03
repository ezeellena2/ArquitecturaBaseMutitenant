using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;

namespace ArquitecturaBaseMultitenant.Application.Services.Auth;

/// <summary>Comprueba límite de organizaciones, estado de alta y disponibilidad del slug.</summary>
internal sealed class BusinessSignupPolicyReader(
    ITenantAdminReader tenantAdmins,
    IPlatformSettingsReader platformSettings,
    ITenantSlugAvailabilityReader slugs)
{
    internal async Task<Result<bool>> EvaluateAsync(Guid userId, TenantSlug slug,
        CancellationToken cancellationToken)
    {
        var settings = await platformSettings.FindAsync(cancellationToken)
            ?? throw new InvalidOperationException("PlatformSettings has not been seeded.");
        var owned = await tenantAdmins.ListOwnedBusinessTenantIdsAsync(userId, cancellationToken);
        var allowed = PlatformSettings.Create(ConsumerSignupMode.Open, settings.BusinessSignup,
            settings.MaxOwnedOrganizations).CanCreateBusinessOrganization(owned.Count);
        if (allowed.IsFailure) return allowed.Error;

        if (await slugs.IsReservedAsync(slug.Value, cancellationToken)) return TenantSlugErrors.Reserved;
        if (await slugs.IsTakenAsync(slug.Value, cancellationToken)) return TenantSlugErrors.Taken;
        return Result.Success(settings.BusinessSignup == BusinessSignupMode.RequiresApproval);
    }
}
