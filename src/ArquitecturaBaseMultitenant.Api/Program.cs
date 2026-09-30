using ArquitecturaBaseMultitenant.Api;
using ArquitecturaBaseMultitenant.Api.Hosting;
using ArquitecturaBaseMultitenant.Api.Localization;
using ArquitecturaBaseMultitenant.Api.Legal;
using ArquitecturaBaseMultitenant.Api.OpenApi;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Rls;
using ArquitecturaBaseMultitenant.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddTrustedForwardedHeaders(builder.Configuration);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddPresentation();

var app = builder.Build();

// El exportador OpenAPI de MSBuild construye el host sin Aspire ni conexiones.
var isOpenApiExporter = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name is "GetDocument.Insider";
if (!isOpenApiExporter)
{
    var runtimeConnection = app.Configuration.GetConnectionString("appdb")
        ?? throw new InvalidOperationException("Missing runtime database connection 'appdb'.");
    if (app.Environment.IsDevelopment())
    {
        await DatabaseBootstrapExtensions.BootstrapAsync(
            app.Configuration.GetConnectionString("postgres-bootstrap")
                ?? throw new InvalidOperationException("Missing bootstrap database connection."),
            app.Configuration.GetConnectionString("appdb-admin")
                ?? throw new InvalidOperationException("Missing administrator database connection."),
            runtimeConnection,
            CancellationToken.None);
    }
    else
    {
        await RuntimeRoleValidator.ValidateAsync(runtimeConnection, CancellationToken.None);
    }

    if (!app.Environment.IsEnvironment("Testing"))
    {
        await app.Services.SeedDatabaseAsync(CancellationToken.None);
    }
}

app.UseForwardedHeaders();
app.UseSecurityHeaders();

SupportedCultures cultures;
await using (var cultureScope = app.Services.CreateAsyncScope())
{
    cultures = await SupportedCultures.LoadAsync(
        cultureScope.ServiceProvider.GetRequiredService<ICultureCatalog>(), CancellationToken.None);
}
app.UseRequestLocalization(cultures.CreateRequestLocalizationOptions());

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
// E7: PublicSiteResolutionMiddleware.
app.UseMiddleware<LegalAcceptanceMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
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
