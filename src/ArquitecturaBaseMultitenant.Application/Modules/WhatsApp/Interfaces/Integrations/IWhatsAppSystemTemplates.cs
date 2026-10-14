using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Integrations;

/// <summary>
/// Las 16 plantillas del sistema (E8, <c>docs/operations/whatsapp-plantillas.md</c>) con su nombre en Meta, que sale de la
/// configuración. Sus textos están en los resources del módulo; acá van los datos y la categoría de cada idioma.
/// </summary>
public interface IWhatsAppSystemTemplates
{
    IReadOnlyList<WhatsAppSystemTemplate> List();
}

/// <summary>Variables son los nombres de los datos ({{1}}, {{2}}…), cuyos textos están en «Template.Variable.{nombre}».</summary>
public sealed record WhatsAppSystemTemplate(string Key, string MetaName, IReadOnlyList<string> Variables,
    IReadOnlyList<WhatsAppSystemTemplateLanguage> Languages);

public sealed record WhatsAppSystemTemplateLanguage(string Culture, WhatsAppTemplateCategory Category);
