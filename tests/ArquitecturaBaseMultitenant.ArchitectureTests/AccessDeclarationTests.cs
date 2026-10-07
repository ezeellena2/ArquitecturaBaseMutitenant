using System.Reflection;
using ArquitecturaBaseMultitenant.Api.Controllers.Account;
using ArquitecturaBaseMultitenant.Api.Controllers.Auth;
using ArquitecturaBaseMultitenant.Api.Controllers.PublicSite;
using ArquitecturaBaseMultitenant.Api.Controllers.ReferenceData;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

/// <summary>
/// Comprueba que cada acción HTTP declare su acceso y que las rutas anónimas pertenezcan a la lista
/// autorizada. Evita publicar operaciones con un alcance ambiguo.
/// </summary>
public sealed class AccessDeclarationTests
{
    private static readonly HashSet<Type> AnonymousMainDomainControllers =
    [
        typeof(ReferenceDataController),
        typeof(LoginCodeController),
        typeof(AccountRecoveryController),
        typeof(ContactController),
        typeof(SignupController),
        typeof(BusinessSignupController),
        typeof(TenantSlugAvailabilityController),
        typeof(LoginMethodsController),
        typeof(InvitationsController),
        typeof(ExternalLoginController),
        typeof(AccountDeletionCancelController),
        typeof(LegalController),
        typeof(ConnectController),
        typeof(DirectoryController),
    ];

    [Fact]
    public void Every_production_action_declares_its_access_mode()
    {
        var controllers = typeof(MeController).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false }
                && type.IsSubclassOf(typeof(ControllerBase)));
        var violations = controllers.SelectMany(controller => controller
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
            .Select(method => Violation(controller, method))
            .Where(message => message is not null));

        Assert.Empty(violations);
    }

    [Fact]
    public void Anonymous_controllers_are_an_explicit_closed_list()
    {
        var actual = typeof(MeController).Assembly.GetTypes()
            .Where(type => type.IsSubclassOf(typeof(ControllerBase))
                && (type.IsDefined(typeof(AllowAnonymousAttribute), inherit: true)
                    || type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                        .Any(method => method.IsDefined(typeof(AllowAnonymousAttribute), inherit: true)))
                && !type.GetCustomAttributes(inherit: true)
                    .Any(attribute => attribute.GetType().Name == "PublicSiteAttribute"))
            .ToHashSet();

        Assert.Equal(AnonymousMainDomainControllers.OrderBy(type => type.FullName),
            actual.OrderBy(type => type.FullName));
    }

    [Fact]
    public void Authenticated_public_site_requires_only_consumer_after_all_access_filters()
    {
        var type = typeof(SharedPublicSiteProbe);
        Assert.Null(Violation(type, type.GetMethod(nameof(SharedPublicSiteProbe.Consumer))!));
        foreach (var name in new[] { nameof(SharedPublicSiteProbe.Business), nameof(SharedPublicSiteProbe.Mixed), nameof(SharedPublicSiteProbe.EmptyIntersection) })
            Assert.NotNull(Violation(type, type.GetMethod(name)!));
        Assert.NotNull(Violation(typeof(PlatformPublicSiteProbe), typeof(PlatformPublicSiteProbe).GetMethod(nameof(PlatformPublicSiteProbe.Platform))!));
    }

    private static string? Violation(Type controller, MethodInfo action)
    {
        var anonymous = Has<AllowAnonymousAttribute>(controller, action);
        var access = Has<AccessAttribute>(controller, action);
        var publicSite = HasNamedAttribute(controller, action, "PublicSiteAttribute");
        if (publicSite)
        {
            if (anonymous)
                return !access ? null : $"{controller.Name}.{action.Name}: anonymous public site cannot declare access";
            var declarations = controller.GetCustomAttributes<AccessAttribute>(inherit: true)
                .Concat(action.GetCustomAttributes<AccessAttribute>(inherit: true)).ToArray();
            if (declarations.Length == 0)
                return $"{controller.Name}.{action.Name}: authenticated public site requires consumer";
            var allowed = declarations[0].Allowed.ToHashSet();
            foreach (var declaration in declarations.Skip(1)) allowed.IntersectWith(declaration.Allowed);
            return allowed.SetEquals([Access.Consumer]) ? null
                : $"{controller.Name}.{action.Name}: authenticated public site requires only consumer";
        }

        if (anonymous)
        {
            return !access && AnonymousMainDomainControllers.Contains(controller) ? null
                : $"{controller.Name}.{action.Name}: anonymous controller is not listed";
        }

        return access ? null : $"{controller.Name}.{action.Name}: missing access declaration";
    }

    private static bool Has<TAttribute>(Type controller, MethodInfo action) where TAttribute : Attribute =>
        controller.IsDefined(typeof(TAttribute), inherit: true)
        || action.IsDefined(typeof(TAttribute), inherit: true);

    private static bool HasNamedAttribute(Type controller, MethodInfo action, string name) =>
        controller.GetCustomAttributes(inherit: true).Any(attribute => attribute.GetType().Name == name)
        || action.GetCustomAttributes(inherit: true).Any(attribute => attribute.GetType().Name == name);

    [PublicSite, Access(Access.Consumer, Access.Business)]
    private sealed class SharedPublicSiteProbe : ControllerBase
    {
        [HttpPost, Access(Access.Consumer)] public OkResult Consumer() => Ok();
        [HttpPost, Access(Access.Business)] public OkResult Business() => Ok();
        [HttpPost, Access(Access.Platform)] public OkResult EmptyIntersection() => Ok();
        [HttpPost] public OkResult Mixed() => Ok();
    }

    [PublicSite, Access(Access.Platform)]
    private sealed class PlatformPublicSiteProbe : ControllerBase
    {
        [HttpPost] public OkResult Platform() => Ok();
    }
}
