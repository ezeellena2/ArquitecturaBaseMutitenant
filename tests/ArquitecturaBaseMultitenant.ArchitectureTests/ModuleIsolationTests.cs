using System.Reflection;
namespace ArquitecturaBaseMultitenant.ArchitectureTests;
/// <summary>Detecta referencias de núcleo hacia un módulo por IL, con un control prohibido.</summary>
public sealed class ModuleIsolationTests
{
    [Fact]
    public void Core_code_does_not_depend_on_optional_modules()
    {
        var assemblies = new[] { "Domain", "Application", "Infrastructure", "Api" }.Select(layer =>
            Assembly.Load("ArquitecturaBaseMultitenant." + layer));
        var violations = assemblies.SelectMany(ArchitectureIl.TypeUses).Where(IsForbidden)
            .Select(use => use.Owner + " -> " + use.Type).Distinct();
        Assert.Empty(violations);
    }
    [Fact]
    public void Detector_rejects_module_dependencies_in_the_core()
    {
        Assert.True(IsForbidden(new("ArquitecturaBaseMultitenant.Application.Services.Identity.UserService",
            "ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Interfaces.Services.IWhatsAppWebhookService")));
        Assert.True(IsForbidden(new("ArquitecturaBaseMultitenant.Infrastructure.Persistence.Reader",
            "ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppContact")));
        Assert.True(IsForbidden(new("ArquitecturaBaseMultitenant.Domain.Authentication.LoginMethod",
            "ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppContact")));
        Assert.False(IsForbidden(new("Program", "ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.WhatsAppApiModule")));
        Assert.False(IsForbidden(new("ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence.ContactRepository",
            "ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppContact")));
        Assert.False(IsForbidden(new("ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppMessage",
            "ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppContact")));
    }
    private static bool IsForbidden(ArchitectureIl.TypeUse use)
    {
        var target = use.Type.Contains(".Modules.", StringComparison.Ordinal)
            || use.Type.StartsWith("ArquitecturaBaseMultitenant.Domain.WhatsApp.", StringComparison.Ordinal);
        return target && !use.Owner.Contains(".Modules.", StringComparison.Ordinal)
            && !use.Owner.StartsWith("ArquitecturaBaseMultitenant.Domain.WhatsApp.", StringComparison.Ordinal)
            && use.Owner != "Program";
    }
}
