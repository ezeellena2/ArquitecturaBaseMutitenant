using System.Reflection;
using ArquitecturaBaseMultitenant.Api.Controllers.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>Guarda exigida por E5: el núcleo permanece disponible y los módulos declaran su catálogo.</summary>
public sealed class ModuleControllersTests
{
    private static readonly HashSet<string> CoreAreas =
    [
        "ArquitecturaBaseMultitenant.Api.Controllers.Account",
        "ArquitecturaBaseMultitenant.Api.Controllers.Auth",
        "ArquitecturaBaseMultitenant.Api.Controllers.ReferenceData",
        "ArquitecturaBaseMultitenant.Api.Controllers.Organization",
        "ArquitecturaBaseMultitenant.Api.Controllers.Platform",
        "ArquitecturaBaseMultitenant.Api.Controllers.PublicSite",
    ];

    [Fact]
    public void Optional_controllers_declare_catalogued_gates_and_the_core_has_none()
    {
        var keys = Domain.Features.Features.All.Select(definition => definition.Key).ToHashSet(StringComparer.Ordinal);
        var controllers = typeof(MeController).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(ControllerBase)));
        foreach (var controller in controllers)
        {
            var gates = controller.GetCustomAttributes<FeatureGateAttribute>(inherit: true).ToArray();
            if (CoreAreas.Contains(controller.Namespace!))
            {
                Assert.Empty(gates);
                Assert.Empty(controller.GetMethods().SelectMany(action => action.GetCustomAttributes<FeatureGateAttribute>(inherit: true)));
                continue;
            }
            Assert.NotEmpty(gates);
            Assert.All(gates.SelectMany(gate => gate.Features), key => Assert.Contains(key, keys));
        }
    }
}
