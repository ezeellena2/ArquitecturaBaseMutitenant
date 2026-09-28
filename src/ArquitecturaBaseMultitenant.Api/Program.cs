using ArquitecturaBaseMultitenant.Api;
using ArquitecturaBaseMultitenant.Api.Hosting;
using ArquitecturaBaseMultitenant.Api.Localization;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Application;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddTrustedForwardedHeaders(builder.Configuration);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddPresentation();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseSecurityHeaders();

var cultures = await SupportedCultures.LoadAsync(
    app.Services.GetRequiredService<ICultureCatalog>(), CancellationToken.None);
app.UseRequestLocalization(cultures.CreateRequestLocalizationOptions());

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

// E3: UseAuthentication → TenantResolutionMiddleware.
// E7: PublicSiteResolutionMiddleware.
// E3: LegalAcceptanceMiddleware → UseAuthorization.

if (app.Environment.IsDevelopment())
{
    // E2: bootstrap de base de datos antes de exponer la documentación.
    app.MapOpenApiDocumentation();
}
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();
// En desarrollo sirve Vite; sin wwwroot no se registra el middleware de archivos estáticos.
if (Directory.Exists(app.Environment.WebRootPath))
{
    app.UseStaticFiles();
}

app.MapDefaultEndpoints();
app.MapControllers();
app.UseSpaFallback();

await app.RunAsync();
