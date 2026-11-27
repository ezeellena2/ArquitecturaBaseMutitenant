using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features;
using ArquitecturaBaseMultitenant.Domain.Features;

namespace ArquitecturaBaseMultitenant.Application.Modules.Forms.Services;

/// <summary>La funcionalidad <c>forms</c>: solo de organizaciones y apagada por defecto (C13).</summary>
internal sealed class FormsFeatureDefinitionContributor : IFeatureDefinitionContributor
{
    public IReadOnlyList<FeatureDefinition> Definitions { get; } = [new("forms", FeatureScope.Business, false)];
}
