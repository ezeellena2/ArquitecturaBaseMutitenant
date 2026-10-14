using ArquitecturaBaseMultitenant.Domain.Auditing;
using ArquitecturaBaseMultitenant.Domain.Common;

namespace ArquitecturaBaseMultitenant.Domain.WhatsApp;

/// <summary>Una plantilla «Para consultas» de Plataforma: solo Utility (Plataforma no tiene marketing, ADR 0038).</summary>
public sealed class PlatformWhatsAppTemplate : WhatsAppTemplateBase, IGlobalSecurityAudited
{
    SecurityEventType IGlobalSecurityAudited.SecurityEventType => SecurityEventType.PlatformSettingsChanged;
    Guid? IGlobalSecurityAudited.SecurityEventTargetTenantId => null;

    private PlatformWhatsAppTemplate() { }

    public static PlatformWhatsAppTemplate Create(string name, string language, string body, IEnumerable<WhatsAppTemplateVariable> variables)
    {
        var template = new PlatformWhatsAppTemplate();
        template.Initialize(name, language, WhatsAppTemplateCategory.Utility, body, variables);
        return template;
    }
}
