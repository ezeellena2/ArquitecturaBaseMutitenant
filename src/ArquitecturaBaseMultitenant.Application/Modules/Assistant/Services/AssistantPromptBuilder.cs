using ArquitecturaBaseMultitenant.Application.Common.Formatting;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Resources;

namespace ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services;

internal static class AssistantPromptBuilder
{
    internal static AssistantPromptContext Build(CultureProfile culture, IReadOnlyList<AssistantUntrustedData> data) =>
        new(AssistantToolTexts.Get("Prompt.System", culture), data.ToArray());
}
