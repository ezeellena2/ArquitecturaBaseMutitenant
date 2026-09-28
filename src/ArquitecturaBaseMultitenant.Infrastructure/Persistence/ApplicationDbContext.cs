using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Infrastructure.Identity;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // El tipo CLR existe para heredar de IdentityUserContext, pero las tablas de cuenta nacen en E3.
        builder.Ignore<ApplicationUser>();
        builder.Ignore<IdentityUserClaim<Guid>>();
        builder.Ignore<IdentityUserLogin<Guid>>();
        builder.Ignore<IdentityUserToken<Guid>>();
        builder.Ignore<IdentityUserPasskey<Guid>>();
        builder.Ignore<IdentityPasskeyData>();

        builder.HasDefaultSchema(Schemas.Platform);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        builder.ApplyIsolationQueryFilters(this);
        TenantIsolationModelValidator.Validate(builder.Model);
    }
}
