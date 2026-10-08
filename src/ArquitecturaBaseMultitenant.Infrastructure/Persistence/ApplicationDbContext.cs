using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Authorization;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Invitations;
using ArquitecturaBaseMultitenant.Domain.Messaging;
using ArquitecturaBaseMultitenant.Domain.PublicSite;
using ArquitecturaBaseMultitenant.Domain.Platform;
using ArquitecturaBaseMultitenant.Domain.Settings;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.AccessIndex;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Conventions;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Extensions;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Reúne las tablas globales y privadas en un único modelo EF. Aplica convenciones, filtros del tenant activo y validación de clasificación antes de que la Api consulte datos.</summary>
public class ApplicationDbContext : IdentityUserContext<ApplicationUser, Guid>, IDataProtectionKeyContext
{
    private readonly IReadOnlyList<IPersistenceModelContributor> contributors;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext,
        IEnumerable<IPersistenceModelContributor>? modelContributors = null)
        : base(options)
    {
        TenantContext = tenantContext;
        contributors = modelContributors?.ToArray() ?? PersistenceModelContributors.Discover();
    }

    protected ApplicationDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        TenantContext = tenantContext;
        contributors = PersistenceModelContributors.Discover();
    }

    protected ITenantContext TenantContext { get; }

    internal Guid? ActiveTenantId => TenantContext.TenantId;
    internal string ContributorCacheKey => string.Join("|", contributors.Select(value => value.GetType().FullName).Order(StringComparer.Ordinal));

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, PersistenceModelCacheKeyFactory>();
    }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<LoginMethod> LoginMethods => Set<LoginMethod>();
    public DbSet<LoginCode> LoginCodes => Set<LoginCode>();
    public DbSet<AccountDeletionCancelTicket> AccountDeletionCancelTickets => Set<AccountDeletionCancelTicket>();
    public DbSet<ConsumerDataRetention> ConsumerDataRetentions => Set<ConsumerDataRetention>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();
    internal DbSet<UserTenantAccess> UserTenantAccesses => Set<UserTenantAccess>();
    internal DbSet<LoginInvitation> LoginInvitations => Set<LoginInvitation>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    internal DbSet<TenantCounters> TenantCounters => Set<TenantCounters>();
    internal DbSet<PendingTenantOwner> PendingTenantOwners => Set<PendingTenantOwner>();
    public DbSet<ReservedTenantSlug> ReservedTenantSlugs => Set<ReservedTenantSlug>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyMembership> CompanyMemberships => Set<CompanyMembership>();
    public DbSet<Invitation> Invitations => Set<Invitation>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();
    public DbSet<PublicPage> PublicPages => Set<PublicPage>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<TenantFeature> TenantFeatures => Set<TenantFeature>();
    public DbSet<PlatformPermissionAssignment> PlatformPermissionAssignments => Set<PlatformPermissionAssignment>();
    public DbSet<AccountRecoveryRequest> AccountRecoveryRequests => Set<AccountRecoveryRequest>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LegalDocumentContent> LegalDocumentContents => Set<LegalDocumentContent>();
    public DbSet<LegalAcceptance> LegalAcceptances => Set<LegalAcceptance>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        EmailConvention.Configure(configurationBuilder);
        PhoneNumberConvention.Configure(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // El ingreso de la plantilla usa código o Google; passkeys no pertenecen a E3.
        builder.Ignore<IdentityUserPasskey<Guid>>();
        builder.Ignore<IdentityPasskeyData>();

        builder.HasDefaultSchema(Schemas.Platform);
        builder.HasDbFunction(() => SearchFunctions.Unaccent(default!));
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly,
            type => type.Namespace?.StartsWith("ArquitecturaBaseMultitenant.Infrastructure.Modules.", StringComparison.Ordinal) != true);
        foreach (var contributor in contributors) contributor.Configure(builder);
        VersionedConvention.Apply(builder);
        builder.ApplyIsolationQueryFilters(this);
        TenantIsolationModelValidator.Validate(builder.Model, PersistenceModelContributors.GlobalEntities(contributors));
    }
}
