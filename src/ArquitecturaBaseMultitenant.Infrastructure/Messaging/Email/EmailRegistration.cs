using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Infrastructure.Messaging.Email;

internal static class EmailRegistration
{
    internal static IServiceCollection AddEmail(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var email = services.AddOptions<EmailOptions>()
            .BindConfiguration(EmailOptions.SectionName)
            .ValidateDataAnnotations();
        if (environment.IsEnvironment("Testing") &&
            configuration[$"{EmailOptions.SectionName}:Delivery"] is null)
        {
            email.PostConfigure(options => options.Delivery = EmailDelivery.PickupDirectory);
        }
        email.ValidateOnStart();

        var smtp = services.AddOptions<SmtpOptions>()
            .BindConfiguration(SmtpOptions.SectionName);
        // La exportación OpenAPI construye el host sin user-secrets ni transporte real.
        if (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is not "GetDocument.Insider")
        {
            smtp.ValidateOnStart();
        }
        services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
        services.AddScoped<IEmailTransport>(provider =>
            provider.GetRequiredService<IOptions<EmailOptions>>().Value.Delivery == EmailDelivery.Smtp
                ? ActivatorUtilities.CreateInstance<SmtpEmailTransport>(provider)
                : ActivatorUtilities.CreateInstance<PickupDirectoryEmailTransport>(provider));
        services.AddScoped<IChannelSender, EmailChannelSender>();
        services.AddScoped<IEmailTemplateRenderer, EmailTemplateRenderer>();
        services.AddScoped<ILoginCodeChannel, EmailLoginCodeChannel>();
        return services;
    }
}
