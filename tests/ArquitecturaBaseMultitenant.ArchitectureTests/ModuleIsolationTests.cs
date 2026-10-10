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
        Assert.True(IsForbidden(new("ArquitecturaBaseMultitenant.Application.Services.Profile.ProfileService",
            "ArquitecturaBaseMultitenant.Domain.Assistant.AssistantErrors")));
        Assert.False(IsForbidden(new("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services.AssistantConversationService",
            "ArquitecturaBaseMultitenant.Domain.Assistant.AssistantErrors")));
        Assert.True(IsForbidden(new("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services.Service",
            "ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Service")));
        Assert.True(IsForbidden(new("ArquitecturaBaseMultitenant.Application.Modules.WhatsApp.Services.Service",
            "ArquitecturaBaseMultitenant.Domain.Assistant.AssistantErrors")));
        Assert.True(IsForbidden(new("ArquitecturaBaseMultitenant.Application.Modules.Assistant.Services.Service",
            "ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppContact")));
        Assert.False(IsForbidden(new("Program", "ArquitecturaBaseMultitenant.Api.Modules.WhatsApp.WhatsAppApiModule")));
        Assert.False(IsForbidden(new("ArquitecturaBaseMultitenant.Infrastructure.Modules.WhatsApp.Persistence.ContactRepository",
            "ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppContact")));
        Assert.False(IsForbidden(new("ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppMessage",
            "ArquitecturaBaseMultitenant.Domain.WhatsApp.WhatsAppContact")));
    }
    private static readonly string[] OptionalDomainPrefixes = new[] { "Application", "Infrastructure", "Api" }
        .SelectMany(layer => Assembly.Load("ArquitecturaBaseMultitenant." + layer).GetTypes())
        .Select(type => System.Text.RegularExpressions.Regex.Match(type.Namespace ?? "", @"^ArquitecturaBaseMultitenant\.[^.]+\.Modules\.([A-Za-z][A-Za-z0-9]*)(?:\.|$)"))
        .Where(match => match.Success).Select(match => "ArquitecturaBaseMultitenant.Domain." + match.Groups[1].Value + ".")
        .Distinct(StringComparer.Ordinal).ToArray();

    private static bool IsForbidden(ArchitectureIl.TypeUse use)
    {
        var target = ModuleKey(use.Type);
        return target is not null && use.Owner != "Program"
            && !string.Equals(ModuleKey(use.Owner), target, StringComparison.Ordinal);
    }
    private static string? ModuleKey(string type)
    {
        var match = System.Text.RegularExpressions.Regex.Match(type,
            @"^ArquitecturaBaseMultitenant\.[^.]+\.Modules\.([A-Za-z][A-Za-z0-9]*)(?:\.|$)");
        if (match.Success) return match.Groups[1].Value;
        var domain = OptionalDomainPrefixes.FirstOrDefault(prefix => type.StartsWith(prefix, StringComparison.Ordinal));
        return domain?.Split('.')[2];
    }
}
