using ArquitecturaBaseMultitenant.Api.Features;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Api.Authorization;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using System.Reflection;
using ArquitecturaBaseMultitenant.Api.Controllers.Account;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement.Mvc;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>Guarda exigida por E5: el núcleo permanece disponible y los módulos declaran su catálogo.</summary>
public sealed partial class ModuleControllersTests
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

    static partial void AddWhatsAppControllers(Microsoft.Extensions.DependencyInjection.IServiceCollection registrations);
    static partial void AddAssistantControllers(Microsoft.Extensions.DependencyInjection.IServiceCollection registrations);

    [Fact]
    public void Optional_controllers_declare_catalogued_gates_and_the_core_has_none()
    {
        var registrations = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(registrations, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        AddWhatsAppControllers(registrations);
        AddAssistantControllers(registrations);
        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(registrations);
        var contributors = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices<ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Features.IFeatureDefinitionContributor>(provider);
        var keys = Domain.Features.Features.All.Concat(contributors.SelectMany(value => value.Definitions)).Select(definition => definition.Key).ToHashSet(StringComparer.Ordinal);
        var controllers = typeof(MeController).Assembly.GetTypes().Where(type => !type.IsAbstract && type.IsSubclassOf(typeof(ControllerBase)));
        foreach (var controller in controllers)
        {
            var gates = controller.GetCustomAttributes<FeatureGateAttribute>(inherit: true).ToArray();
            if (CoreAreas.Contains(controller.Namespace!))
            {
                Assert.Empty(gates);
                Assert.Null(controller.GetCustomAttribute<ArquitecturaBaseMultitenant.Api.Features.InstalledModuleRouteAttribute>(inherit: true));
                Assert.Empty(controller.GetMethods().SelectMany(action => action.GetCustomAttributes<FeatureGateAttribute>(inherit: true)));
                continue;
            }
            var installed = controller.GetCustomAttribute<ArquitecturaBaseMultitenant.Api.Features.InstalledModuleRouteAttribute>(inherit: true);
            if (installed is not null)
            {
                Assert.True(IsValidInstalledController(controller));
                Assert.Empty(gates);
                var routes = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices<ArquitecturaBaseMultitenant.Api.Features.IInstalledModuleRouteAvailability>(provider);
                Assert.Single(routes, route => route.Key == installed.Key);
                continue;
            }
            Assert.NotEmpty(gates);
            Assert.All(gates.SelectMany(gate => gate.Features), key => Assert.Contains(key, keys));
        }
    }

    [Theory]
    [InlineData(typeof(ConsumerInstalledControl))]
    [InlineData(typeof(BusinessInstalledControl))]
    [InlineData(typeof(PlatformWithoutPermissionControl))]
    [InlineData(typeof(AnonymousPlatformControl))]
    [InlineData(typeof(MixedAccessControl))]
    [InlineData(typeof(TechnicalAccessControl))]
    [InlineData(typeof(TechnicalPermissionControl))]
    [InlineData(typeof(PlatformActionAnonymousControl))]
    [InlineData(typeof(PlatformActionWrongPermissionControl))]
    [InlineData(typeof(ChatConsumerControl))]
    public void Installed_routes_do_not_exempt_access_or_permission_guards(Type controller) =>
        Assert.False(IsValidInstalledController(controller));

    [Fact]
    public void Technical_webhook_and_platform_action_permissions_are_distinct_categories()
    {
        Assert.True(IsValidInstalledController(typeof(TechnicalWebhookControl)));
        Assert.True(IsValidInstalledController(typeof(PlatformActionPermissionControl)));
    }

    private static bool IsValidInstalledController(Type controller)
    {
        var installed = controller.GetCustomAttribute<InstalledModuleRouteAttribute>(true);
        if (installed is null || controller.GetCustomAttributes<FeatureGateAttribute>(true).Any()) return false;
        var actions = controller.GetMethods().Where(method => method.GetCustomAttributes<HttpMethodAttribute>(true).Any()).ToArray();
        if (actions.Any(action => action.GetCustomAttributes<FeatureGateAttribute>(true).Any())) return false;
        var access = controller.GetCustomAttribute<AccessAttribute>(true);
        var anonymous = controller.GetCustomAttribute<AllowAnonymousAttribute>(true) is not null;
        if (installed.Category == InstalledModuleRouteCategory.TechnicalWebhook)
            return anonymous && access is null && controller.GetCustomAttribute<AuthorizeAttribute>(true) is null
                && actions.All(action => action.GetCustomAttribute<AccessAttribute>(true) is null && !action.GetCustomAttributes<AuthorizeAttribute>(true).Any());
        if (installed.Category is not (InstalledModuleRouteCategory.PlatformChat or InstalledModuleRouteCategory.PlatformManagement)) return false;
        if (anonymous || access is null || !access.Allowed.SequenceEqual(new[] { Access.Platform })) return false;
        var controllerPermission = controller.GetCustomAttribute<HasPlatformPermissionAttribute>(true);
        return (actions.Length > 0 || controllerPermission is not null) && actions.All(action =>
            action.GetCustomAttribute<AllowAnonymousAttribute>(true) is null
            && (action.GetCustomAttribute<AccessAttribute>(true) is not { } actionAccess || actionAccess.Allowed.SequenceEqual(new[] { Access.Platform }))
            && (controllerPermission is not null || action.GetCustomAttribute<HasPlatformPermissionAttribute>(true) is not null));
    }

    [InstalledModuleRoute("control", InstalledModuleRouteCategory.PlatformManagement)][Access(Access.Consumer)]
    private sealed class ConsumerInstalledControl { }
    [InstalledModuleRoute("control", InstalledModuleRouteCategory.PlatformManagement)][Access(Access.Business)]
    private sealed class BusinessInstalledControl { }
    [InstalledModuleRoute("control", InstalledModuleRouteCategory.PlatformManagement)][Access(Access.Platform)]
    private sealed class PlatformWithoutPermissionControl { }
    [InstalledModuleRoute("control", InstalledModuleRouteCategory.PlatformManagement)][Access(Access.Platform)][AllowAnonymous][HasPlatformPermission("control")]
    private sealed class AnonymousPlatformControl { }
    [InstalledModuleRoute("control", InstalledModuleRouteCategory.PlatformManagement)][Access(Access.Consumer, Access.Platform)][HasPlatformPermission("control")]
    private sealed class MixedAccessControl { }
    [InstalledModuleRoute("control")][AllowAnonymous]
    private sealed class TechnicalWebhookControl { }
    [InstalledModuleRoute("control", InstalledModuleRouteCategory.PlatformManagement)][Access(Access.Platform)]
    private sealed class PlatformActionPermissionControl
    {
        [HttpGet][HasPlatformPermission("control")]
        public static void Read() { }
    }

    [InstalledModuleRoute("control")][AllowAnonymous][Access(Access.Consumer)]
    private sealed class TechnicalAccessControl { }
    [InstalledModuleRoute("control")][AllowAnonymous][HasPlatformPermission("control")]
    private sealed class TechnicalPermissionControl { }
    [InstalledModuleRoute("control", InstalledModuleRouteCategory.PlatformChat)][Access(Access.Consumer)][HasPlatformPermission("control")]
    private sealed class ChatConsumerControl { }
    [InstalledModuleRoute("control", InstalledModuleRouteCategory.PlatformChat)][Access(Access.Platform)][HasPlatformPermission("control")]
    private sealed class PlatformActionAnonymousControl
    {
        [HttpGet][AllowAnonymous]
        public static void Read() { }
    }
    [InstalledModuleRoute("control", InstalledModuleRouteCategory.PlatformManagement)][Access(Access.Platform)]
    private sealed class PlatformActionWrongPermissionControl
    {
        [HttpGet][HasPermission("control")]
        public static void Read() { }
    }
}
