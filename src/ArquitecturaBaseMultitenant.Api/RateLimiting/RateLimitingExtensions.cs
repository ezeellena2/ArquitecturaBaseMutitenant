using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ArquitecturaBaseMultitenant.Api.RateLimiting;

/// <summary>Registra ventanas independientes por IP para códigos e invitaciones.</summary>
internal static class RateLimitingExtensions
{
    public static IServiceCollection AddLoginRateLimitingPolicies(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>()
            .BindConfiguration(RateLimitingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<RateLimiterOptions>(options =>
        {
            options.AddPolicy(RateLimitPolicies.LoginCode, context =>
            {
                var settings = SettingsOf(context);
                return FixedWindowByIp(context, settings.LoginCodePermitLimit, settings.LoginCodeWindowMinutes);
            });

            options.AddPolicy(RateLimitPolicies.LoginVerify, context =>
            {
                var settings = SettingsOf(context);
                return FixedWindowByIp(context, settings.LoginVerifyPermitLimit, settings.LoginVerifyWindowMinutes);
            });

            options.AddPolicy(RateLimitPolicies.InvitationPreview, context =>
            {
                var settings = SettingsOf(context);
                return FixedWindowByIp(context, settings.InvitationPreviewPermitLimit, settings.InvitationPreviewWindowMinutes);
            });

            options.AddPolicy(RateLimitPolicies.InvitationAccept, context =>
            {
                var settings = SettingsOf(context);
                return FixedWindowByIp(context, settings.InvitationAcceptPermitLimit, settings.InvitationAcceptWindowMinutes);
            });
        });

        return services;
    }

    private static RateLimitingOptions SettingsOf(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;

    private static RateLimitPartition<string> FixedWindowByIp(HttpContext context, int permitLimit, int windowMinutes) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(windowMinutes),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
}
