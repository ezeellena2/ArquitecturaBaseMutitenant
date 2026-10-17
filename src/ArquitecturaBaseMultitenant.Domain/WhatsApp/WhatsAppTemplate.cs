using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Una plantilla de la organización: va a Meta en cada cuenta de sus números, Utility o Marketing.</summary>
public sealed class WhatsAppTemplate : WhatsAppTemplateBase, ITenantOwned
{
    private WhatsAppTemplate() { }

    public Guid TenantId { get; private set; }

    public static WhatsAppTemplate Create(string name, string language, WhatsAppTemplateCategory category, string body,
        IEnumerable<WhatsAppTemplateVariable> variables)
    {
        var template = new WhatsAppTemplate();
        template.Initialize(name, language, category, body, variables);
        return template;
    }
}
