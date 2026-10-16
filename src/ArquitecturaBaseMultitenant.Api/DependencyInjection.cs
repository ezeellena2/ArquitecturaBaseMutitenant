using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Features;
using Microsoft.FeatureManagement.Mvc;
using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Api.Authentication;
using ArquitecturaBaseMultitenant.Api.Idempotency;
using ArquitecturaBaseMultitenant.Api.Json;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.RequestContext;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api;

/// <summary>Registra los adaptadores HTTP de la API y uniforma JSON, errores y OpenAPI. Las cuotas las aplica RateLimitMiddleware (E9).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpContextAccessor();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IDisabledFeaturesHandler, DisabledFeatureHandler>();
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "__Host-MtSignupCsrf";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
            options.FormFieldName = "__RequestVerificationToken";
            options.HeaderName = "RequestVerificationToken";
        });
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<OpenIdPrincipalFactory>();
        services.AddScoped<PendingDeletionCookie>();
        services.AddScoped<IRequestInfo, RequestInfo>();
        services.AddSingleton<IPublicOrigin, PublicOrigin>();
        services.AddScoped<PublicSiteContext>();
        services.AddScoped<IPublicSiteContext>(provider => provider.GetRequiredService<PublicSiteContext>());
        services.AddScoped<IInvitationFlowContext, InvitationFlowContext>();

        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            ProblemDetailsMapper.CompleteFrameworkProblem(context.ProblemDetails);
            ProblemDetailsMapper.AddTraceId(context.ProblemDetails, context.HttpContext);
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddScoped<IdempotencyFilter>();
        services.AddScoped<IdempotentRequestContext>();
        services.AddScoped<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency.IIdempotentRequestContext>(
            provider => provider.GetRequiredService<IdempotentRequestContext>());
        services.AddScoped<LoginLinkReplayVerifier>();
        services.AddScoped<IPlatformFactorBrowser, PlatformFactorCookie>();
        services.AddScoped<RecentPlatformProofFilter>();
        services.AddScoped<PlatformFactorDurableAdapter>();
        services.AddScoped<PlatformReauthenticationDurableAdapter>();
        foreach (var kind in PlatformFactorReferences.Kinds)
        {
            services.AddScoped<IIdempotentReplayResolver>(provider =>
                new PlatformFactorReplayAdapter(provider.GetRequiredService<IPlatformFactorDurableService>(), kind));
        }
        services.AddOpenApiDocumentation();
        services.ConfigureHttpJsonOptions(options => JsonConfiguration.ConfigureJson(options.SerializerOptions));
        services.AddControllers(options =>
            {
                options.Filters.Add(new EmptyJsonBodyContentTypeFilter());
                // E9: la prueba reciente, después del acceso de cada controller y antes de la idempotencia.
                options.Filters.AddService<RecentPlatformProofFilter>(RecentPlatformProofFilter.Order);
            })
            .AddJsonOptions(options => JsonConfiguration.ConfigureJson(options.JsonSerializerOptions));
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = MvcInvalidModelStateResponseFactory.Create);

        return services;
    }
}
