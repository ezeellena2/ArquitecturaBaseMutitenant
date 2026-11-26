using System.Reflection;
using System.Text.Json.Serialization;
using ArquitecturaBaseMultitenant.Application.Models.AssistantActions;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

// La parte del asistente de las acciones declaradas (ADR 0068): cada pantalla del núcleo se traduce a una ruta del módulo,
// del lado del acceso de la acción, y cada acción entra en una propuesta. Sale con el módulo.
public sealed partial class AssistantActionDeclarationTests
{
    static partial void ContributeAssistantNotDeclarable(List<NotDeclarableRoute> routes) => routes.AddRange(
    [
        new(null, "api/platform/assistant/settings", "claves: la del proveedor de IA"),
        new(null, "api/organization/assistant/settings", "claves: la del proveedor de IA"),
    ]);

    [Fact]
    public void Every_screen_translates_to_a_route_of_the_assistant() =>
        Assert.All(Enum.GetValues<AssistantActionScreen>(),
            screen => Assert.True(Enum.IsDefined(AssistantActionScreenRoutes.RouteOf(screen)), screen.ToString()));

    [Fact]
    public void Every_action_opens_on_a_route_of_its_access()
    {
        var violations = DeclaredActions()
            .Where(action => !RouteName(AssistantActionScreenRoutes.RouteOf(action.Screen))
                .StartsWith(AccessPrefix(action.Requirement.Kind), StringComparison.Ordinal))
            .Select(action => $"{action.Key}: la pantalla {action.Screen} abre en una ruta de otro acceso");

        Assert.Empty(violations);
    }

    [Fact]
    public void Every_action_fits_in_a_proposal()
    {
        var oversized = DeclaredActions()
            .Where(action => action.Fields.Sum(field => 1 + (field.ItemFields?.Count ?? 0)) > AssistantTechnicalLimits.MaxProposalFields)
            .Select(action => action.Key);

        Assert.Empty(oversized);
    }

    private static string RouteName(AssistantRouteKey route) =>
        typeof(AssistantRouteKey).GetField(route.ToString())!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name;

    private static string AccessPrefix(AssistantActionRequirementKind kind) => kind switch
    {
        AssistantActionRequirementKind.Consumer => "personal.",
        AssistantActionRequirementKind.Platform => "platform.",
        _ => "business.",
    };
}
