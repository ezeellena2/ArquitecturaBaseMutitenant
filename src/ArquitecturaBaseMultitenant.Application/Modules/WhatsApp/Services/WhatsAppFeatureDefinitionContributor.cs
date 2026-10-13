using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Features;
using ArquitecturaBaseMultitenant.Domain.WhatsApp;

namespace ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services;

/// <summary>La atención por WhatsApp es un módulo que Plataforma prende por organización; «Mensajes» existe para las personas.</summary>
internal sealed class WhatsAppFeatureDefinitionContributor : IFeatureDefinitionContributor
{
    public IReadOnlyList<FeatureDefinition> Definitions { get; } = [new(WhatsAppFeature.Key, FeatureScope.Both, false)];
}
