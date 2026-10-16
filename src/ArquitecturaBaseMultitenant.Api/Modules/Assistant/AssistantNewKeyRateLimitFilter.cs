using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Api.Modules.Assistant.Contracts;
using ArquitecturaBaseMultitenant.Api.RateLimiting;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Settings;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Settings;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

/// <summary>
/// La cuota assistantNewKey (E9): cuenta cuando la configuración del Asistente trae una clave nueva, después de autorizar y
/// antes de que el servicio la pruebe con el proveedor. Conservar o borrar la clave no cuenta.
/// </summary>
internal sealed class AssistantNewKeyRateLimitFilter(IRateLimitService rateLimits) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (context.ActionArguments.Values.Any(IsNewKey))
        {
            var http = context.HttpContext;
            var admitted = await rateLimits.AdmitAsync(new RateLimitRequest([RateLimitProfile.AssistantNewKey],
                http.Connection.RemoteIpAddress?.ToString(), RateLimitMiddleware.SignedUserId(http.User), null),
                http.RequestAborted);
            if (admitted.IsFailure)
            {
                context.Result = admitted.ToActionResult((ControllerBase)context.Controller);
                return;
            }
        }

        await next();
    }

    private static bool IsNewKey(object? argument) => argument switch
    {
        UpdatePlatformAssistantSettingsHttpRequest platform => platform.KeyAction == AssistantKeyAction.Replace,
        UpdateOrganizationAssistantSettingsHttpRequest organization => organization.KeyAction == AssistantKeyAction.Replace,
        _ => false,
    };
}

/// <summary>Aplica <see cref="AssistantNewKeyRateLimitFilter"/> después de los demás filtros de la acción.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
internal sealed class AssistantNewKeyRateLimitAttribute : ServiceFilterAttribute
{
    public AssistantNewKeyRateLimitAttribute() : base(typeof(AssistantNewKeyRateLimitFilter)) => Order = 1000;
}
