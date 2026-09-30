using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ArquitecturaBaseMultitenant.Application.Configuration.Auth;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Identity;
using ArquitecturaBaseMultitenant.Application.Models.Auth;
using ArquitecturaBaseMultitenant.Application.Models.Identity;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;

namespace ArquitecturaBaseMultitenant.Infrastructure.Identity;

/// <summary>Configura Identity, los esquemas de autenticación y el acceso con Google. Las rutas API validan tokens de OpenIddict, mientras las cookies sirven solo al recorrido de conexión.</summary>
internal static class IdentityRegistration
{
    private const string GoogleSection = "Authentication:Google";
    private const string DataProtectionCertificateKey = "DataProtection:Certificate:Base64";
    private const string DataProtectionPasswordKey = "DataProtection:Certificate:Password";

    public static IServiceCollection AddIdentityServices(this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment,
        bool? isOpenApiExporter = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

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

        var dataProtection = services.AddDataProtection()
            .SetApplicationName("ArquitecturaBaseMultitenant");
        if (isOpenApiExporter ??
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider")
        {
            // MSBuild construye el host para exportar OpenAPI sin appdb: no necesita claves persistentes.
            dataProtection.UseEphemeralDataProtectionProvider();
        }
        else
        {
            dataProtection.PersistKeysToDbContext<ApplicationDbContext>();
            if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
            {
                dataProtection.ProtectKeysWithCertificate(LoadDataProtectionCertificate(configuration));
            }
        }

        services.AddScoped<ISignInService, SignInService>();
        services.AddScoped<IUserLookup, UserLookup>();
        services.AddScoped<IUserStatusReader, UserStatusReader>();
        return services;
    }

    private static X509Certificate2 LoadDataProtectionCertificate(IConfiguration configuration)
    {
        var base64 = configuration[DataProtectionCertificateKey];
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException($"Missing {DataProtectionCertificateKey} outside Development and Testing.");
        }

        byte[] pfx;
        try
        {
            pfx = Convert.FromBase64String(base64);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException($"Invalid {DataProtectionCertificateKey}.", exception);
        }

        try
        {
            var certificate = X509CertificateLoader.LoadPkcs12(pfx,
                configuration[DataProtectionPasswordKey]);
            if (certificate.HasPrivateKey)
            {
                return certificate;
            }

            certificate.Dispose();
            throw new InvalidOperationException($"{DataProtectionCertificateKey} must contain a private key.");
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException($"Invalid {DataProtectionCertificateKey} or {DataProtectionPasswordKey}.",
                exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pfx);
        }
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
            options.Events.OnRedirectToAuthorizationEndpoint = context =>
            {
                if (context.Properties.Items.ContainsKey(GoogleAccountLinkState.UserIdKey))
                {
                    context.Response.Headers.CacheControl = "no-store";
                    return context.Response.WriteAsJsonAsync(new GoogleChallengeResponse(context.RedirectUri));
                }
                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                var destination = context.Properties?.Items.ContainsKey(GoogleAccountLinkState.UserIdKey) == true
                    ? "/cuenta"
                    : context.Properties?.Items.TryGetValue("Access", out var access) == true && access == "business"
                        ? "/login/empresa" : ReturnUrls.LoginPath;
                context.Response.Redirect(
                    destination + "?error=" + Uri.EscapeDataString("Auth.ExternalLogin.Failed"));
                context.HandleResponse();
                return Task.CompletedTask;
            };
        });

        services.AddOptions<GoogleOptions>(GoogleDefaults.AuthenticationScheme).ValidateOnStart();
    }
}
