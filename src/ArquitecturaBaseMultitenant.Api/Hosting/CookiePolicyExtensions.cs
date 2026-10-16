using Microsoft.AspNetCore.CookiePolicy;

namespace ArquitecturaBaseMultitenant.Api.Hosting;

/// <summary>
/// La política única de cookies (E9): toda cookie que escribe la Api es Secure y HttpOnly, con SameSite Lax como mínimo
/// (Strict se conserva) y sin Domain, así ninguna se amplía a los subdominios de las organizaciones. Lax deja pasar las
/// navegaciones de vuelta de Google (un GET a /signin-google) y la entrada a /connect/authorize.
/// </summary>
internal static class CookiePolicyExtensions
{
    public static IServiceCollection AddHostOnlyCookiePolicy(this IServiceCollection services) =>
        services.Configure<CookiePolicyOptions>(options =>
        {
            options.Secure = CookieSecurePolicy.Always;
            options.HttpOnly = HttpOnlyPolicy.Always;
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
            options.OnAppendCookie = context => context.CookieOptions.Domain = null;
            options.OnDeleteCookie = context => context.CookieOptions.Domain = null;
        });
}
