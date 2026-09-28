using ArquitecturaBaseMultitenant.Api;
using ArquitecturaBaseMultitenant.Api.Localization;
using ArquitecturaBaseMultitenant.Application;
using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Interfaces.ReferenceData;
using ArquitecturaBaseMultitenant.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddPresentation();

var app = builder.Build();

var cultures = await SupportedCultures.LoadAsync(
    app.Services.GetRequiredService<ICultureCatalog>(), CancellationToken.None);
app.UseRequestLocalization(cultures.CreateRequestLocalizationOptions());

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapDefaultEndpoints();
app.MapControllers();

await app.RunAsync();
