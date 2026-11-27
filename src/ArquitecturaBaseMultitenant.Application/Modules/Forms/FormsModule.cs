using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Localization;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Configuration;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Resources;
using ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms;

/// <summary>El módulo Formularios (E12): nace con los catálogos; el núcleo nunca lo referencia.</summary>
public static class FormsModule
{
    public static IServiceCollection AddFormsModule(this IServiceCollection services)
    {
        services.AddOptions<FormsOptions>().BindConfiguration(FormsOptions.SectionName)
            .Validate(FormsOptions.IsValid, "Forms:CatalogFiles needs at least one file operation per organization and room for one organization in the installation.")
            .ValidateOnStart();
        services.AddSingleton<IFeatureDefinitionContributor, FormsFeatureDefinitionContributor>();
        services.AddSingleton<IErrorTextSource, FormsErrorTextSource>();
        services.AddScoped<IFormCatalogService, FormCatalogService>();
        services.AddScoped<FormCatalogItemEditor>();
        services.AddScoped<FormCatalogPositionGuard>();
        services.AddScoped<FormCatalogPositionEditor>();
        services.AddScoped<IFormCatalogSourceService, FormCatalogSourceService>();
        services.AddScoped<FormCatalogFiltersPolicy>();
        services.AddScoped<FormCatalogPreviewBuilder>();
        services.AddScoped<IFormCatalogImportService, FormCatalogImportService>();
        services.AddScoped<FormCatalogFileResolver>();
        services.AddScoped<IAssistantActionSource, FormCatalogActionSource>();
        return services;
    }
}
