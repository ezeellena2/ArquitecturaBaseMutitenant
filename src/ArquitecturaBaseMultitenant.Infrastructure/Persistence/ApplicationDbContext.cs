using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Domain.Authentication;
using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Legal;
using ArquitecturaBaseMultitenant.Domain.Messaging;
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

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityUserContext<ApplicationUser, Guid>, IDataProtectionKeyContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        TenantContext = tenantContext;
    }

    protected ApplicationDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        TenantContext = tenantContext;
    }

    protected ITenantContext TenantContext { get; }

    internal Guid? ActiveTenantId => TenantContext.TenantId;

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<LoginMethod> LoginMethods => Set<LoginMethod>();
    public DbSet<LoginCode> LoginCodes => Set<LoginCode>();
    public DbSet<ReauthTicket> ReauthTickets => Set<ReauthTicket>();
    public DbSet<LoginAudit> LoginAudits => Set<LoginAudit>();
    internal DbSet<UserTenantAccess> UserTenantAccesses => Set<UserTenantAccess>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<PlatformSettings> PlatformSettings => Set<PlatformSettings>();
    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LegalDocumentContent> LegalDocumentContents => Set<LegalDocumentContent>();
    public DbSet<LegalAcceptance> LegalAcceptances => Set<LegalAcceptance>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        EmailConvention.Configure(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // El ingreso de la plantilla usa código o Google; passkeys no pertenecen a E3.
        builder.Ignore<IdentityUserPasskey<Guid>>();
        builder.Ignore<IdentityPasskeyData>();

        builder.HasDefaultSchema(Schemas.Platform);
        builder.HasDbFunction(() => SearchFunctions.Unaccent(default!));
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        VersionedConvention.Apply(builder);
        builder.ApplyIsolationQueryFilters(this);
        TenantIsolationModelValidator.Validate(builder.Model);
    }
}
