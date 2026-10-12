using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ArquitecturaBaseMultitenant.Api.ErrorHandling;
using ArquitecturaBaseMultitenant.Application.Interfaces.Integrations.Idempotency;
using ArquitecturaBaseMultitenant.Application.Models.Idempotency;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Interfaces.Services;
using ArquitecturaBaseMultitenant.Application.Modules.Assistant.Models;
using ArquitecturaBaseMultitenant.Domain.Results;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace ArquitecturaBaseMultitenant.Api.Modules.Assistant;

internal sealed class AssistantStreamResult(AssistantTurnAdmission admission) : IActionResult, IIdempotentStreamResult
{
    public IdempotentReference Reference => admission.Reference;
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        try { await TypedResults.ServerSentEvents(FramesAsync(http, http.RequestAborted)).ExecuteAsync(http); }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested) { }
        // Disconnect ends this reader only. No Stop call and no worker lifetime linked to RequestAborted.
    }
    private async IAsyncEnumerable<SseItem<JsonElement>> FramesAsync(HttpContext http, [EnumeratorCancellation] CancellationToken ct)
    {
        var service = http.RequestServices.GetRequiredService<IAssistantTurnService>();
        var clock = http.RequestServices.GetRequiredService<TimeProvider>();
        var lastHeartbeat = clock.GetTimestamp();
        var sequence = -1;
        while (!ct.IsCancellationRequested)
        {
            var read = await service.ReadAsync(admission.TurnId, sequence, ct);
            if (read.IsFailure)
            {
                var data = new AssistantErrorEventResponse(admission.TurnId, read.Error.Code, http.TraceIdentifier, null, AssistantFailedStatus.Failed);
                yield return new SseItem<JsonElement>(AssistantTurnJson.Frame("error", sequence + 1, data).Data, "error")
                    { EventId = admission.TurnId.ToString("D") + ":" + (sequence + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) };
                await http.Response.Body.FlushAsync(ct);
                yield break;
            }
            // Every emitted frame has just passed authorization and privacy, even when a batch contains many events.
            if (read.Value.Frames.Count > 0)
            {
                var frame = read.Value.Frames[0];
                sequence = frame.Sequence;
                yield return new SseItem<JsonElement>(frame.Data, frame.Event)
                    { EventId = admission.TurnId.ToString("D") + ":" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                await http.Response.Body.FlushAsync(ct);
                lastHeartbeat = clock.GetTimestamp();
                if (frame.Event is "done" or "error") yield break;
                continue;
            }
            if (read.Value.Terminal) yield break;
            if (clock.GetElapsedTime(lastHeartbeat) >= TimeSpan.FromSeconds(15))
            {
                await http.Response.WriteAsync(": heartbeat\n\n", ct);
                await http.Response.Body.FlushAsync(ct);
                lastHeartbeat = clock.GetTimestamp();
            }
            await Task.Delay(TimeSpan.FromMilliseconds(100), clock, ct);
        }
    }
}
internal static class AssistantStreamResults
{
    internal static IActionResult ToStreamResult(this Result<AssistantTurnAdmission> result, ControllerBase controller) =>
        result.IsFailure ? result.ToActionResult(controller) : new AssistantStreamResult(result.Value);
}
