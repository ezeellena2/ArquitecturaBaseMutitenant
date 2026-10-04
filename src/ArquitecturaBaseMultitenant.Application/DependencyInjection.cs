using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Common.Validation;
using ArquitecturaBaseMultitenant.Application.Services.Auditing;
using ArquitecturaBaseMultitenant.Application.Services.Auth;
using ArquitecturaBaseMultitenant.Application.Services.Companies;
using ArquitecturaBaseMultitenant.Application.Services.Home;
using ArquitecturaBaseMultitenant.Application.Services.Identity;
using ArquitecturaBaseMultitenant.Application.Services.Invitations;
using ArquitecturaBaseMultitenant.Application.Services.Legal;
using ArquitecturaBaseMultitenant.Application.Services.Messaging;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationSettings;
using ArquitecturaBaseMultitenant.Application.Services.OrganizationUsers;
using ArquitecturaBaseMultitenant.Application.Services.Platform;
using ArquitecturaBaseMultitenant.Application.Services.Profile;
using ArquitecturaBaseMultitenant.Application.Services.PublicSite;
using ArquitecturaBaseMultitenant.Application.Services.ReferenceData;
using ArquitecturaBaseMultitenant.Application.Services.Roles;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application;

/// <summary>
/// Registra los casos de uso, sus colaboradores, las opciones y la validación de Application para que la Api los
/// consuma mediante interfaces. Cada área tiene su <c>*Registration.cs</c>, explícito y sin reflexión.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // «Para resolver» (ADR 0040) junta los pendientes en el orden en que se registran sus fuentes: Usuarios,
        // Configuración y Página pública. Un módulo suma la suya en su propio registro.
        services
            .AddReferenceData()
            .AddRoles()
            .AddHome()
            .AddOrganizationUsers()
            .AddOrganizationSettings()
            .AddPublicSite()
            .AddCompanies()
            .AddAuditing()
            .AddPlatform()
            .AddMessaging()
            .AddAuth()
            .AddProfile()
            .AddLegal()
            .AddInvitations()
            .AddIdentity();
        services.AddScoped<DisplayFormatter>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<IRequestValidator, RequestValidator>();

        return services;
    }
}
