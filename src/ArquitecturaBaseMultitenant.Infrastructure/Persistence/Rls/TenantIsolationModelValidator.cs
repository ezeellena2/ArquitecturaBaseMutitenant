using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using OpenIddict.EntityFrameworkCore.Models;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;

internal static class TenantIsolationModelValidator
{
    public static void Validate(IReadOnlyModel model)
    {
        foreach (var entity in model.GetEntityTypes().Where(type => type.BaseType is null && !type.IsOwned()))
        {
            var type = entity.ClrType;
            var isTenant = typeof(ITenantOwned).IsAssignableFrom(type);
            var isPublic = typeof(IPublishedByBusiness).IsAssignableFrom(type);
            var isShared = typeof(IConsumerBusinessShared).IsAssignableFrom(type);
            var categoryCount = (isTenant ? 1 : 0) + (isPublic ? 1 : 0) + (isShared ? 1 : 0);
            var schema = entity.GetSchema();
            var filters = entity.GetDeclaredQueryFilters().Select(filter => filter.Key).ToHashSet(StringComparer.Ordinal);
            var requiredFilter = isTenant ? ModelBuilderExtensions.TenantFilter
                : isPublic ? ModelBuilderExtensions.PublicFilter
                : isShared ? ModelBuilderExtensions.PartiesFilter : null;
            var expectedSchema = isTenant ? Schemas.Tenant
                : isPublic ? Schemas.PublicSite
                : isShared ? Schemas.Engagement : null;

            if (categoryCount > 1 || (categoryCount == 0 && !IsKnownGlobal(type)) ||
                (categoryCount == 1 && (schema != expectedSchema || !filters.Contains(requiredFilter!))) ||
                (categoryCount == 0 && (schema is not (Schemas.Platform or Schemas.Identity) ||
                    filters.Overlaps([ModelBuilderExtensions.TenantFilter,
                        ModelBuilderExtensions.PublicFilter, ModelBuilderExtensions.PartiesFilter]))))
            {
                throw new InvalidOperationException(
                    $"Entity {type.FullName} in schema {schema ?? "<none>"} has an invalid data classification or isolation filter.");
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(type) && !filters.Contains(ModelBuilderExtensions.SoftDeleteFilter))
            {
                throw new InvalidOperationException($"Entity {type.Name} is soft deletable but has no SoftDelete filter.");
            }
        }
    }

    private static bool IsKnownGlobal(Type type) =>
        type == typeof(DataProtectionKey) ||
        type == typeof(ApplicationUser) ||
        type == typeof(LoginMethod) ||
        type == typeof(LoginCode) ||
        type == typeof(LoginAudit) ||
        type == typeof(UserTenantAccess) ||
        type == typeof(Tenant) ||
        type == typeof(PlatformSettings) ||
        type == typeof(SecurityEvent) ||
        type == typeof(OutboxMessage) ||
        type == typeof(LegalDocument) ||
        type == typeof(LegalDocumentContent) ||
        type == typeof(LegalAcceptance) ||
        type == typeof(IdentityUserClaim<Guid>) ||
        type == typeof(IdentityUserLogin<Guid>) ||
        type == typeof(IdentityUserToken<Guid>) ||
        type == typeof(OpenIddictEntityFrameworkCoreApplication<Guid>) ||
        type == typeof(OpenIddictEntityFrameworkCoreAuthorization<Guid>) ||
        type == typeof(OpenIddictEntityFrameworkCoreScope<Guid>) ||
        type == typeof(OpenIddictEntityFrameworkCoreToken<Guid>) ||
        type.Namespace == typeof(Currency).Namespace ||
        type.FullName == "ArquitecturaBaseMultitenant.Infrastructure.Idempotency.IdempotencyKey";
}
