using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;

/// <summary>Registra los usuarios de la organización (miembros, invitaciones y roles), con su fuente de «Para resolver».</summary>
internal static class OrganizationUsersRegistration
{
    internal static IServiceCollection AddOrganizationUsers(this IServiceCollection services)
    {
        services.AddScoped<IPendingItemSource, OrganizationUserPendingItems>();
        services.AddScoped<IOrganizationUserService, OrganizationUserService>();
        services.AddScoped<OrganizationUserAssignmentManager>();
        services.AddScoped<OrganizationUserAdministratorGuard>();
        services.AddScoped<OrganizationUserAccessCleaner>();
        services.AddScoped<OrganizationUserInvitationManager>();
        services.AddScoped<OrganizationMembershipEndNoticeContextProvider>();
        services.AddScoped<OrganizationAvailableLoginMethodsResolver>();
        services.AddScoped<OrganizationMembershipEndNotifier>();
        services.AddScoped<OrganizationUserEditor>();
        services.AddScoped<OrganizationUserStatusEditor>();
        services.AddScoped<OrganizationUserRemover>();
        return services;
    }
}
