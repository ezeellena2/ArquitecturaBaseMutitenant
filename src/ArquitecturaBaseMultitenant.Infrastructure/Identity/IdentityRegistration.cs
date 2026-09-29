using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

internal static class IdentityRegistration
{
    private const string GoogleSection = "Authentication:Google";

    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // AddIdentityCore evita que la cookie autentique las rutas /api: allí se valida el bearer de OpenIddict.
        var authentication = services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        authentication.AddIdentityCookies();
        AddGoogle(services, authentication, configuration);

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.User.AllowedUserNameCharacters = string.Empty;
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager();

        services.AddOptions<IdentityOptions>().Configure<IOptions<LoginCodeOptions>>((identity, loginCode) =>
        {
            identity.Lockout.MaxFailedAccessAttempts = loginCode.Value.LockoutMaxFailedAttempts;
            identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(loginCode.Value.LockoutMinutes);
        });
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

        services.AddDataProtection()
            .SetApplicationName("ArquitecturaBaseMultitenant")
            .PersistKeysToDbContext<ApplicationDbContext>();

        services.AddScoped<ISignInService, SignInService>();
        services.AddScoped<IUserLookup, UserLookup>();
        return services;
    }

    private static void AddGoogle(
        IServiceCollection services,
        AuthenticationBuilder authentication,
        IConfiguration configuration)
    {
        var clientId = configuration[GoogleSection + ":ClientId"];
        services.AddSingleton<IGoogleAvailability>(new GoogleAvailability(!string.IsNullOrWhiteSpace(clientId)));
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return;
        }

        authentication.AddGoogle(options =>
        {
            options.ClientId = clientId;
            options.ClientSecret = configuration[GoogleSection + ":ClientSecret"]
                is { Length: > 0 } secret ? secret
                : throw new InvalidOperationException("Missing Authentication:Google:ClientSecret.");
            options.SignInScheme = IdentityConstants.ExternalScheme;
            options.ClaimActions.MapJsonKey("email_verified", "email_verified");
            options.Events.OnRemoteFailure = context =>
            {
                context.Response.Redirect(
                    ReturnUrls.LoginPath + "?error=" + Uri.EscapeDataString("Auth.ExternalLogin.Failed"));
                context.HandleResponse();
                return Task.CompletedTask;
            };
        });

        services.AddOptions<GoogleOptions>(GoogleDefaults.AuthenticationScheme).ValidateOnStart();
    }
}
