using ArquitecturaBaseMultitenant.Domain.Common;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.ReferenceData;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class DataClassificationTests
{
    // Excepciones globales explícitas de E2. Cada tipo nuevo se clasifica o se agrega con su motivo.
    private static readonly HashSet<string> GlobalTypes =
    [
        typeof(DataProtectionKey).FullName!,
        typeof(ApplicationUser).FullName!,
        "ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex.UserTenantAccess",
        typeof(LoginMethod).FullName!,
        typeof(LoginCode).FullName!,
        typeof(ReauthTicket).FullName!, // Comprobante global de cuenta, ligado a usuario y acción.
        typeof(LoginAudit).FullName!,
        typeof(Tenant).FullName!,
        typeof(PlatformSettings).FullName!,
        typeof(SecurityEvent).FullName!,
        typeof(OutboxMessage).FullName!,
        typeof(LegalDocument).FullName!,
        typeof(LegalDocumentContent).FullName!,
        typeof(LegalAcceptance).FullName!,
        typeof(IdentityUserClaim<Guid>).FullName!,
        typeof(IdentityUserLogin<Guid>).FullName!,
        typeof(IdentityUserToken<Guid>).FullName!,
        "ArquitecturaBaseMultitenant.Infrastructure.Idempotency.IdempotencyKey",
        typeof(Currency).FullName!,
        typeof(CurrencyTranslation).FullName!,
        typeof(Country).FullName!,
        typeof(CountryTranslation).FullName!,
        typeof(ReferenceTimeZone).FullName!,
        typeof(TimeZoneCountry).FullName!,
        typeof(TimeZoneTranslation).FullName!,
        typeof(Culture).FullName!,
        typeof(CultureTranslation).FullName!,
        typeof(TaxIdType).FullName!,
        typeof(TaxIdTypeTranslation).FullName!,
    ];

    [Fact]
    public void Every_mapped_entity_has_one_class_schema_and_filter()
    {
        var entities = ArchitectureModel.EntityTypes()
            .Where(entity => entity.BaseType is null && !entity.IsOwned())
            .ToArray();

        Assert.NotEmpty(entities);
        Assert.Empty(entities.Select(entity => DescribeViolation(entity.ClrType, entity.GetSchema(),
                entity.GetDeclaredQueryFilters().Select(filter => filter.Key!)))
            .OfType<string>());
    }

    [Fact]
    public void Detector_rejects_unclassified_misplaced_and_unfiltered_entities()
    {
        Assert.NotNull(DescribeViolation(typeof(UnclassifiedProbe), "platform", []));
        Assert.NotNull(DescribeViolation(typeof(PrivateProbe), "platform", ["Tenant"]));
        Assert.NotNull(DescribeViolation(typeof(PrivateProbe), "tenant", []));
        Assert.NotNull(DescribeViolation(typeof(PublicProbe), "public_site", ["Tenant"]));
        Assert.NotNull(DescribeViolation(typeof(SharedProbe), "engagement", ["Parties", "Public"]));
        Assert.NotNull(DescribeViolation(typeof(MixedProbe), "tenant", ["Tenant"]));

        Assert.Null(DescribeViolation(typeof(PrivateProbe), "tenant", ["Tenant"]));
        Assert.Null(DescribeViolation(typeof(PublicProbe), "public_site", ["Public"]));
        Assert.Null(DescribeViolation(typeof(SharedProbe), "engagement", ["Parties"]));
    }

    private static string? DescribeViolation(Type type, string? schema, IEnumerable<string> filterNames)
    {
        var hasTenant = typeof(ITenantOwned).IsAssignableFrom(type);
        var hasPublic = typeof(IPublishedByBusiness).IsAssignableFrom(type);
        var hasParties = typeof(IConsumerBusinessShared).IsAssignableFrom(type);
        var categories = (hasTenant ? 1 : 0) + (hasPublic ? 1 : 0) + (hasParties ? 1 : 0);
        var filters = filterNames.ToHashSet(StringComparer.Ordinal);
        var expectedSchema = hasTenant ? "tenant" : hasPublic ? "public_site"
            : hasParties ? "engagement"
            : IsIdentityGlobal(type) ? "identity" : "platform";
        var expectedFilter = hasTenant ? "Tenant" : hasPublic ? "Public"
            : hasParties ? "Parties" : null;
        var isolationFilters = new[] { "Tenant", "Public", "Parties" };

        if (categories > 1 || categories == 0 && !GlobalTypes.Contains(type.FullName!))
        {
            return $"{type.FullName}: missing or conflicting data classification.";
        }

        if (schema != expectedSchema || isolationFilters
            .Where(filter => filters.Contains(filter))
            .Where(filter => filter != expectedFilter)
            .Any() || expectedFilter is not null && !filters.Contains(expectedFilter))
        {
            return $"{type.FullName}: expected schema {expectedSchema} and filter {expectedFilter ?? "<none>"}.";
        }

        if (typeof(ISoftDeletable).IsAssignableFrom(type) && !filters.Contains("SoftDelete"))
        {
            return $"{type.FullName}: missing SoftDelete filter.";
        }

        return null;
    }

    private static bool IsIdentityGlobal(Type type) => type == typeof(ApplicationUser)
        || type.FullName == "ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex.UserTenantAccess"
        || type == typeof(LoginMethod) || type == typeof(LoginCode) || type == typeof(LoginAudit) || type == typeof(ReauthTicket)
        || type == typeof(IdentityUserClaim<Guid>) || type == typeof(IdentityUserLogin<Guid>)
        || type == typeof(IdentityUserToken<Guid>) || type == typeof(LegalAcceptance);

#pragma warning disable CA1812 // Casos de control que solo se inspeccionan por tipo.
    private sealed class UnclassifiedProbe;

    private sealed class PrivateProbe : ITenantOwned
    {
        public Guid TenantId => Guid.Empty;
    }

    private sealed class PublicProbe : IPublishedByBusiness
    {
        public Guid BusinessTenantId => Guid.Empty;

        public bool IsPublished => false;
    }

    private sealed class SharedProbe : IConsumerBusinessShared
    {
        public Guid ConsumerTenantId => Guid.Empty;

        public Guid BusinessTenantId => Guid.Empty;
    }

    private sealed class MixedProbe : ITenantOwned, IPublishedByBusiness
    {
        public Guid TenantId => Guid.Empty;

        public Guid BusinessTenantId => Guid.Empty;

        public bool IsPublished => false;
    }
#pragma warning restore CA1812
}
