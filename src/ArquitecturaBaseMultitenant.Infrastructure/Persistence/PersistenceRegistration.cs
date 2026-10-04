using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure.Caching;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Interceptors;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Readers;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Repositories;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;
using ArquitecturaBaseMultitenant.Infrastructure.ReferenceData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ArquitecturaBaseMultitenant.Infrastructure.Persistence;

/// <summary>Compone el DbContext, el límite de guardado y los adaptadores de lectura y escritura. Separa el proveedor técnico del seed con mt_owner de la conexión operativa con mt_app.</summary>
internal static class PersistenceRegistration
{
    /// <summary>Scope técnico del bootstrap: siembra con mt_owner, nunca con mt_app.</summary>
    internal static ServiceProvider CreateReferenceSeedProvider(string adminConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adminConnectionString);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCaching();
        services.AddScoped<TenantContext>(_ => new TenantContext());
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddDbContext<ApplicationDbContext>((_, options) => options
            .UseNpgsql(adminConnectionString)
            .UseOpenIddict<Guid>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ReferenceDataSeeder>();
        services.AddSingleton<JsonReferenceDataCatalog>();
        services.AddReferenceCatalogs();
        return services.BuildServiceProvider();
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<TenantContext>(provider => new TenantContext(() =>
            provider.GetRequiredService<ApplicationDbContext>().Database.CurrentTransaction is not null));
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantScope>(provider => provider.GetRequiredService<TenantContext>());
        services.AddScoped<ITenantAccessInitializer>(provider => provider.GetRequiredService<TenantContext>());
        services.TryAddScoped<ICurrentUser, SystemCurrentUser>();
        services.TryAddScoped<IRequestInfo, SystemCurrentUser>();
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddScoped<TenantStampInterceptor>();
        services.AddScoped<SoftDeleteInterceptor>();
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditTrailInterceptor>();
        services.AddScoped<IAuditLog, AuditLog>();

        services.AddDbContext<ApplicationDbContext>((provider, options) =>
            options.UseNpgsql(configuration.GetConnectionString("appdb")
                    ?? throw new InvalidOperationException(
                        "Missing connection string 'ConnectionStrings:appdb'. Start the API from the AppHost."))
                .UseOpenIddict<Guid>()
                .AddInterceptors(
                    provider.GetRequiredService<TenantConnectionInterceptor>(),
                    provider.GetRequiredService<TenantStampInterceptor>(),
                    provider.GetRequiredService<SoftDeleteInterceptor>(),
                    provider.GetRequiredService<AuditableEntityInterceptor>(),
                    provider.GetRequiredService<AuditTrailInterceptor>()));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IPlatformTenantRepository, PlatformTenantRepository>();
        services.AddScoped<IPlatformReader, PlatformReader>();
        services.AddScoped<ITenantReader, TenantReader>();
        services.AddScoped<ITenantSlugAvailabilityReader, TenantSlugAvailabilityReader>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanyReader, CompanyReader>();
        services.AddScoped<ICompanyMembershipRepository, CompanyMembershipRepository>();
        services.AddScoped<ICompanyMembershipReader, CompanyMembershipReader>();
        services.AddScoped<IOrganizationUserReader, OrganizationUserReader>();
        services.AddScoped<ICompanyAdminReader, CompanyAdminReader>();
        services.AddScoped<ICompanyAdminLock, CompanyAdminLock>();
        services.AddScoped<IMemberReader, MemberReader>();
        services.AddScoped<IInvitationRepository, InvitationRepository>();
        services.AddScoped<IInvitationReader, InvitationReader>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRoleAssignmentRepository, RoleAssignmentRepository>();
        services.AddScoped<IRoleReader, RoleReader>();
        services.AddScoped<ITenantAdminLock, TenantAdminLock>();
        services.AddScoped<UserTenantAccessReader>();
        services.AddScoped<IUserTenantAccessReader>(provider => provider.GetRequiredService<UserTenantAccessReader>());
        services.AddScoped<ITenantAdminReader>(provider => provider.GetRequiredService<UserTenantAccessReader>());
        services.AddScoped<IPersonalSpaceLock, PersonalSpaceLock>();
        services.AddScoped<ITenantSettingsRepository, TenantSettingsRepository>();
        services.AddScoped<ITenantSettingsReader, TenantSettingsReader>();
        services.AddScoped<ITenantProvisioningReader, TenantProvisioningReader>();
        services.AddScoped<ITenantDomainRepository, TenantDomainRepository>();
        services.AddScoped<ITenantDomainReader, TenantDomainReader>();
        services.AddScoped<IPublicPageRepository, PublicPageRepository>();
        services.AddScoped<IPublicPageReader, PublicPageReader>();
        services.AddScoped<IOrganizationAuditReader, OrganizationAuditReader>();
        services.AddScoped<TenantSettingsLoader>();
        services.AddScoped<IPlatformSettingsRepository, PlatformSettingsRepository>();
        services.AddScoped<ITenantFeatureRepository, TenantFeatureRepository>();
        services.AddScoped<ITenantFeatureReader, TenantFeatureReader>();
        services.AddScoped<IPlatformPermissionAssignmentRepository, PlatformPermissionAssignmentRepository>();
        services.AddScoped<IPlatformPermissionAssignmentReader, PlatformPermissionAssignmentReader>();
        services.AddScoped<IPlatformSettingsReader, PlatformSettingsReader>();
        services.AddScoped<ISecurityEventRepository, SecurityEventRepository>();
        services.AddScoped<ILegalRepository, LegalRepository>();
        services.AddScoped<ILegalReader, LegalReader>();
        services.AddScoped<IAccountDeletionTenantReader, AccountDeletionTenantReader>();
        services.AddScoped<IAccountDeletionRepository, AccountDeletionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IExternalLoginLock, ExternalLoginLock>();
        services.AddScoped<ILoginMethodRepository, LoginMethodRepository>();
        services.AddScoped<ILoginMethodReader, LoginMethodReader>();
        services.AddScoped<ILoginMethodMembershipReader, LoginMethodMembershipReader>();
        services.AddScoped<ILoginCodeRepository, LoginCodeRepository>();
        services.AddScoped<IReauthTicketRepository, ReauthTicketRepository>();
        services.AddScoped<ILoginAuditRepository, LoginAuditRepository>();
        services.AddSingleton<JsonReferenceDataCatalog>();
        services.AddScoped<ReferenceDataSeeder>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<OpenIddictSeeder>();
        services.AddScoped<PlatformSeeder>();
        services.AddScoped<LegalDocumentSeeder>();
        services.AddReferenceCatalogs();

        return services;
    }

    private static IServiceCollection AddReferenceCatalogs(this IServiceCollection services)
    {
        services.AddScoped<ReferenceDataReader>();
        services.AddScoped<ICurrencyCatalog>(provider => provider.GetRequiredService<ReferenceDataReader>());
        services.AddScoped<ICountryCatalog>(provider => provider.GetRequiredService<ReferenceDataReader>());
        services.AddScoped<ITimeZoneCatalog>(provider => provider.GetRequiredService<ReferenceDataReader>());
        services.AddScoped<ICultureCatalog>(provider => provider.GetRequiredService<ReferenceDataReader>());
        services.AddScoped<ITaxIdTypeCatalog>(provider => provider.GetRequiredService<ReferenceDataReader>());

        return services;
    }
}
