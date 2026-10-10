using ArquitecturaBaseMultitenant.Api.Tenancy;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Request;
using ArquitecturaBaseMultitenant.Application.Interfaces.Persistence;
using ArquitecturaBaseMultitenant.Application.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Models.Request;
using ArquitecturaBaseMultitenant.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.IntegrationTests.TestFeatures.BusinessAccess;

/// <summary>Captura sólo desde el pipeline firmado real; no existe en la Api productiva.</summary>
[ApiController]
[Authorize]
[Access(Access.Consumer, Access.Business, Access.Platform)]
[Route("_test/execution-actor")]
public sealed class ExecutionActorProbeController(IExecutionActorFactory factory, IExecutionActorScope scopes,
    ICurrentUser currentUser, ITenantScope tenantScopes, ITenantContext tenant, IFeatureService features) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ExecutionActorCapture>> CaptureAsync(CancellationToken ct)
    {
        var captured = await factory.CaptureAsync(ct);
        if (captured.IsFailure) return StatusCode(403);
        var id = currentUser.UserId;
        var rejected = false;
        try { using var scope = scopes.Enter(captured.Value); }
        catch (InvalidOperationException) { rejected = true; }
        var kindlessDenied = true;
        if (captured.Value.TenantId is { } tenantId)
        {
            using (tenantScopes.Enter(tenantId))
                kindlessDenied = tenant.TenantKind is null && !await features.IsEnabledAsync("assistant", ct);
        }
        return new ExecutionActorCapture(captured.Value, rejected && id == currentUser.UserId, kindlessDenied);
    }
}

public sealed record ExecutionActorCapture(ExecutionActor Actor, bool HttpScopeRejected, bool KindlessAccessDenied);
