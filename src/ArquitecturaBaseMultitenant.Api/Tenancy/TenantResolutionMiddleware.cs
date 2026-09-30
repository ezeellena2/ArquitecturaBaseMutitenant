using System.Globalization;
using System.Security.Claims;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Caching;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Domain.Results;
using ArquitecturaBaseMultitenant.Domain.Tenancy;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace ArquitecturaBaseMultitenant.Api.Tenancy;

/// <summary>Resuelve exclusivamente los claims del acceso firmado y valida sus estados vigentes.</summary>
public sealed class TenantResolutionMiddleware(RequestDelegate next, ITenantStatusCache tenantStatuses)
{
    public async Task InvokeAsync(HttpContext context, IAccessStatusCache accessStatuses,
        ITenantAccessInitializer tenantInitializer, ITenantReader tenantReader)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null
            && context.GetEndpoint()?.Metadata.GetMetadata<AccessAttribute>() is null)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var principal = context.User;
        var userIdText = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdText, CultureInfo.InvariantCulture, out var userId) || userId == Guid.Empty)
        {
            await RejectAsync(context, AccessErrors.Wrong);
            return;
        }

        var userStatus = await accessStatuses.GetUserStatusAsync(userId, context.RequestAborted);
        if (userStatus != UserStatus.Active)
        {
            await RejectAsync(context, Error.Unauthorized(ApiErrorCodes.Unauthorized,
                "The account is not active."));
            return;
        }

        if (context.Request.Path.StartsWithSegments("/connect"))
        {
            await next(context);
            return;
        }

        var accessText = principal.FindFirstValue(TenantClaimTypes.Access);
        if (string.IsNullOrWhiteSpace(accessText) && IsEntryRoute(context))
        {
            await next(context);
            return;
        }

        if (!Enum.TryParse<Access>(accessText, true, out var access) || !Enum.IsDefined(access))
        {
            await RejectAsync(context, AccessErrors.Wrong);
            return;
        }

        if (access == Access.Platform)
        {
            await next(context);
            return;
        }

        if (!Guid.TryParse(principal.FindFirstValue(TenantClaimTypes.TenantId), CultureInfo.InvariantCulture,
                out var tenantId) || tenantId == Guid.Empty
            || !Enum.TryParse<TenantKind>(principal.FindFirstValue(TenantClaimTypes.TenantKind), true,
                out var kind) || !Enum.IsDefined(kind)
            || (access == Access.Consumer && kind != TenantKind.Personal)
            || (access == Access.Business && kind != TenantKind.Business))
        {
            await RejectAsync(context, AccessErrors.Wrong);
            return;
        }

        if (HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path.Equals(new PathString("/api/me")))
        {
            // La propia cuenta permite abandonar una organización inactiva. Su proyección no concede permisos.
            tenantInitializer.SetFromAccess(tenantId, kind);
            await next(context);
            return;
        }

        var tenantStatus = await tenantStatuses.GetStatusAsync(tenantId, context.RequestAborted);
        var tenantError = tenantStatus switch
        {
            TenantStatus.Active => null,
            TenantStatus.Suspended => TenantErrors.Suspended,
            TenantStatus.PendingApproval => TenantErrors.PendingApproval,
            TenantStatus.Provisioning => TenantErrors.Provisioning,
            TenantStatus.Closed => TenantErrors.Closed,
            _ => AccessErrors.NotMember,
        };
        if (tenantError is not null)
        {
            if (tenantStatus is TenantStatus.Suspended or TenantStatus.PendingApproval
                or TenantStatus.Provisioning or TenantStatus.Closed)
            {
                var tenant = await tenantReader.FindByIdAsync(tenantId, context.RequestAborted);
                if (tenant is not null)
                {
                    tenantError = tenantError with
                    {
                        Metadata = new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["organizationName"] = tenant.Name,
                            ["tenantId"] = tenantId.ToString("D", CultureInfo.InvariantCulture),
                        },
                    };
                }
            }

            await RejectAsync(context, tenantError);
            return;
        }

        var memberStatus = await accessStatuses.GetMemberStatusAsync(userId, tenantId, kind, context.RequestAborted);
        var memberError = memberStatus switch
        {
            MemberStatus.Active => null,
            MemberStatus.Inactive => MemberErrors.Inactive,
            _ => AccessErrors.NotMember,
        };
        if (memberError is not null)
        {
            await RejectAsync(context, memberError);
            return;
        }

        tenantInitializer.SetFromAccess(tenantId, kind);
        await next(context);
    }

    private static async Task RejectAsync(HttpContext context, Error error)
    {
        var factory = context.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        var problem = ProblemDetailsMapper.FromError(error, factory, context);
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = problem,
            });
    }

    private static bool IsEntryRoute(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null
        || context.Request.Path.StartsWithSegments("/connect")
        || context.Request.Path.StartsWithSegments("/account")
        || context.Request.Path.StartsWithSegments("/api/auth");
}
