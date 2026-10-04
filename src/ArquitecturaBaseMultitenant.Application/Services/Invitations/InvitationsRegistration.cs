using ArquitecturaBaseMultitenant.Application.Configuration.Invitations;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Services.Invitations;

/// <summary>Registra las invitaciones (emitir, mostrar y aceptar), con sus opciones.</summary>
internal static class InvitationsRegistration
{
    internal static IServiceCollection AddInvitations(this IServiceCollection services)
    {
        services.AddOptions<InvitationOptions>()
            .BindConfiguration(InvitationOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<InvitationIssuer>();
        services.AddScoped<InvitationIssuingGuard>();
        services.AddScoped<InvitationDeliveryIssuer>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<InvitationPreviewBuilder>();
        services.AddScoped<InvitationPreviewFlow>();
        services.AddScoped<InvitationAcceptanceResolver>();
        services.AddScoped<InvitationAcceptanceGuard>();
        services.AddScoped<InvitationIdentityDefaults>();
        services.AddScoped<InvitationAccountRegistrar>();
        services.AddScoped<InvitationAcceptor>();
        services.AddScoped<InvitationSessionRestorer>();
        return services;
    }
}
