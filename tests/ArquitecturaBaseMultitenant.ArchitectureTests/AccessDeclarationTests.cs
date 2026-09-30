using System.Reflection;
using ArquitecturaBaseMultitenant.Api.Controllers.Account;
using ArquitecturaBaseMultitenant.Api.Controllers.Auth;
using ArquitecturaBaseMultitenant.Api.Controllers.ReferenceData;
using ArquitecturaBaseMultitenant.Api.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ArquitecturaBaseMultitenant.ArchitectureTests;

public sealed class AccessDeclarationTests
{
    private static readonly HashSet<Type> AnonymousMainDomainControllers =
    [
        typeof(ReferenceDataController),
        typeof(LoginCodeController),
        typeof(SignupController),
        typeof(LoginMethodsController),
        typeof(ExternalLoginController),
        typeof(AccountDeletionCancelController),
        typeof(LegalController),
        typeof(ConnectController),
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

    private static string? Violation(Type controller, MethodInfo action)
    {
        var anonymous = Has<AllowAnonymousAttribute>(controller, action);
        var access = Has<AccessAttribute>(controller, action);
        var publicSite = HasNamedAttribute(controller, action, "PublicSiteAttribute");
        if (publicSite)
        {
            return anonymous && !access ? null
                : $"{controller.Name}.{action.Name}: public site requires anonymous without access";
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
}
